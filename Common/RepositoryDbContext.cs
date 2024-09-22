using System;
using Microsoft.EntityFrameworkCore;

namespace Common
{
    public class RepositoryDbContext : DbContext
    {
        public RepositoryDbContext(DbContextOptions<RepositoryDbContext> options)
            : base(options)
        {
        }

        public RepositoryDbContext()
        {
        }

        public DbSet<Repository> Repositories { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Configure the database provider and connection string
                var databasePath = @"C:\repos\Builds.db";
                var connectionString = $"Data Source={databasePath}";
                optionsBuilder.UseSqlite(connectionString);
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Repository>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Project)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Url)
                .IsRequired();

            entity.Property(e => e.CloneUrl)
                .IsRequired();

            entity.Property(e => e.MasterClonned)
                .IsRequired();

            entity.Ignore(e => e.Path); // Path is a computed property, not stored in the database

            // Configure relationships if any
            // For example, if Repository has a one-to-many relationship with another entity, configure it here
        });
    }
}
