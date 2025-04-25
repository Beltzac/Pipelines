using Common.Utils;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TUnit.Core;

namespace Tests.Common.Utils
{
    /// <summary>
    /// Tests targeting the *re‑written* DbContextUpsertExtensions (see sibling canvas file).
    /// Each test spins up a fresh in‑memory SQLite database, exercises a scenario, and then
    /// verifies the resulting state.  Tracking clashes that previously broke several tests
    /// have been removed by **re‑creating detached graphs** after clearing the ChangeTracker.
    /// </summary>
    public class DbContextUpsertExtensionsTests
    {
        #region Test infrastructure ---------------------------------------------------------
        private class TestEntity
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public TestEntity? Child { get; set; }
            public ICollection<TestEntity> Children { get; set; } = new List<TestEntity>();
        }

        private class TestDbContext : DbContext
        {
            public DbSet<TestEntity> TestEntities { get; set; } = null!;

            public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

            protected override void OnModelCreating(ModelBuilder mb)
            {
                mb.Entity<TestEntity>().HasKey(e => e.Id);
                mb.Entity<TestEntity>().Property(e => e.Name).IsRequired(false);
            }
        }

        private static TestDbContext CreateTestContext()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();

            var opts = new DbContextOptionsBuilder<TestDbContext>()
                            .UseSqlite(connection)
                            .EnableSensitiveDataLogging()
                            .Options;
            var ctx = new TestDbContext(opts);
            ctx.Database.EnsureCreated();
            return ctx;
        }
        #endregion

        // ---------------------------------------------------------------------------------
        // 1. Insert root entity
        // ---------------------------------------------------------------------------------
        [Test]
        public async Task UpsertGraphAsync_NewEntity_AddsEntityToDatabase()
        {
            using var db = CreateTestContext();
            var entity = new TestEntity { Id = 1, Name = "Test" };

            await db.UpsertGraphAsync(entity);

            var added = await db.TestEntities.FindAsync(1);
            added.Should().NotBeNull();
            added!.Name.Should().Be("Test");
        }

        // ---------------------------------------------------------------------------------
        // 2. Update existing root entity
        // ---------------------------------------------------------------------------------
        [Test]
        public async Task UpsertGraphAsync_ExistingEntity_UpdatesEntityInDatabase()
        {
            using var db = CreateTestContext();

            // Seed row
            db.TestEntities.Add(new TestEntity { Id = 2, Name = "Original" });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            // Re‑hydrated graph (detached)
            var updated = new TestEntity { Id = 2, Name = "Updated" };
            await db.UpsertGraphAsync(updated);

            (await db.TestEntities.FindAsync(2))!.Name.Should().Be("Updated");
        }

        // ---------------------------------------------------------------------------------
        // 3. WalkGraph should traverse all nodes w/out duplicates
        // ---------------------------------------------------------------------------------
        [Test]
        public void WalkGraph_TraversesAllEntities()
        {
            using var db = CreateTestContext();
            var root = new TestEntity
            {
                Id = 1,
                Name = "Root",
                Child = new TestEntity { Id = 2, Name = "Child" },
                Children = new List<TestEntity>
                {
                    new TestEntity { Id = 3, Name = "Child1" },
                    new TestEntity { Id = 4, Name = "Child2" }
                }
            };

            DbContextUpsertExtensions.WalkGraph(db, root).Should().HaveCount(4);
        }

        // ---------------------------------------------------------------------------------
        // 4. Verify collection ADD
        // ---------------------------------------------------------------------------------
        [Test]
        public async Task UpsertGraphAsync_AddItemsToList_UpdatesCollectionInDatabase()
        {
            using var db = CreateTestContext();

            db.TestEntities.Add(new TestEntity { Id = 1, Name = "Parent" });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var parent = new TestEntity
            {
                Id = 1,
                Name = "Parent",
                Children = new List<TestEntity>
                {
                    new TestEntity { Id = 2, Name = "Child1" },
                    new TestEntity { Id = 3, Name = "Child2" }
                }
            };

            await db.UpsertGraphAsync(parent);

            var dbParent = await db.TestEntities.Include(e => e.Children).FirstAsync(e => e.Id == 1);
            dbParent.Children.Should().ContainSingle(c => c.Name == "Child1")
                               .And.ContainSingle(c => c.Name == "Child2");
        }

        // ---------------------------------------------------------------------------------
        // 5. Verify collection REMOVE
        // ---------------------------------------------------------------------------------
        [Test]
        public async Task UpsertGraphAsync_RemoveItemsFromList_UpdatesCollectionInDatabase()
        {
            using var db = CreateTestContext();

            db.TestEntities.Add(new TestEntity
            {
                Id = 1,
                Name = "Parent",
                Children = new List<TestEntity>
                {
                    new TestEntity { Id = 2, Name = "Child1" },
                    new TestEntity { Id = 3, Name = "Child2" }
                }
            });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            // Re‑create detached graph with only remaining child
            var parent = new TestEntity
            {
                Id = 1,
                Name = "Parent",
                Children = new List<TestEntity>
                {
                    new TestEntity { Id = 3, Name = "Child2" }
                }
            };

            await db.UpsertGraphAsync(parent);

            var dbParent = await db.TestEntities.Include(e => e.Children).FirstAsync(e => e.Id == 1);
            dbParent.Children.Should().HaveCount(1);
            dbParent.Children.First().Name.Should().Be("Child2");
        }

        // ---------------------------------------------------------------------------------
        // 6. Verify collection UPDATE
        // ---------------------------------------------------------------------------------
        [Test]
        public async Task UpsertGraphAsync_UpdateItemsInList_UpdatesCollectionInDatabase()
        {
            using var db = CreateTestContext();

            db.TestEntities.Add(new TestEntity
            {
                Id = 1,
                Name = "Parent",
                Children = new List<TestEntity>
                {
                    new TestEntity { Id = 2, Name = "Child1" },
                    new TestEntity { Id = 3, Name = "Child2" }
                }
            });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var parent = new TestEntity
            {
                Id = 1,
                Name = "Parent",
                Children = new List<TestEntity>
                {
                    new TestEntity { Id = 2, Name = "UpdatedChild" },
                    new TestEntity { Id = 3, Name = "Child2" }
                }
            };

            await db.UpsertGraphAsync(parent);

            var dbParent = await db.TestEntities.Include(e => e.Children).FirstAsync(e => e.Id == 1);
            dbParent.Children.Should().ContainSingle(c => c.Name == "UpdatedChild");
        }

        // ---------------------------------------------------------------------------------
        // 7. Verify collection MIXED ADD/UPDATE/REMOVE
        // ---------------------------------------------------------------------------------
        [Test]
        public async Task UpsertGraphAsync_MixedListOperations_UpdatesCollectionInDatabase()
        {
            using var db = CreateTestContext();

            db.TestEntities.Add(new TestEntity
            {
                Id = 1,
                Name = "Parent",
                Children = new List<TestEntity>
                {
                    new TestEntity { Id = 2, Name = "Child1" },
                    new TestEntity { Id = 3, Name = "Child2" }
                }
            });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var parent = new TestEntity
            {
                Id = 1,
                Name = "Parent",
                Children = new List<TestEntity>
                {
                    new TestEntity { Id = 3, Name = "UpdatedChild2" }, // Update existing
                    new TestEntity { Id = 4, Name = "NewChild" }        // Add new
                }
            };

            await db.UpsertGraphAsync(parent);

            var dbParent = await db.TestEntities.Include(e => e.Children).FirstAsync(e => e.Id == 1);
            dbParent.Children.Should().HaveCount(2);
            dbParent.Children.Should().ContainSingle(c => c.Id == 3 && c.Name == "UpdatedChild2");
            dbParent.Children.Should().ContainSingle(c => c.Id == 4 && c.Name == "NewChild");
        }
    }
}
