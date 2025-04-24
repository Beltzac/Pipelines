using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Utils
{
    public static class DbContextUpsertExtensions
    {
        public static async Task UpsertGraphAsync<T>(
            this DbContext db, T root, CancellationToken ct = default)
            where T : class
        {
            // Remove deleted children in collection navigations for root
            var rootEntry = db.Entry(root);
            foreach (var nav in rootEntry.Navigations.Where(n => n.Metadata.IsCollection))
            {
                var collectionEntry = rootEntry.Collection(nav.Metadata.Name);
                // Load existing child keys without tracking
                var existingKeys = await collectionEntry.Query()
                    .Cast<object>()
                    .Select(e => EF.Property<object>(e, "Id"))
                    .ToListAsync(ct);
                // Current child instances in memory
                var current = ((IEnumerable)collectionEntry.CurrentValue ?? Enumerable.Empty<object>())
                    .Cast<object>()
                    .ToList();
                var currentKeys = new HashSet<object>(current
                    .Select(c => db.Entry(c).Property("Id").CurrentValue));
                // Delete children removed from the collection
                foreach (var id in existingKeys.Where(id => !currentKeys.Contains(id)))
                {
                    var elementType = nav.Metadata.TargetEntityType.ClrType;
                    var stub = Activator.CreateInstance(elementType)!;
                    elementType.GetProperty("Id")!.SetValue(stub, id);
                    db.Entry(stub).State = EntityState.Deleted;
                }
            }
// Apply deletions before processing graph updates
await db.SaveChangesAsync(ct);
db.ChangeTracker.Clear();

// Reattach root and set foreign keys for collection navigations
var rootEntryAfterDelete = db.Entry(root);
var rootKeyName = rootEntryAfterDelete.Metadata.FindPrimaryKey().Properties.Single().Name;
foreach (var navAfter in rootEntryAfterDelete.Navigations.Where(n => n.Metadata.IsCollection))
{
    var fkName = navAfter.Metadata.ForeignKey.Properties.Single().Name;
    var children = ((IEnumerable)rootEntryAfterDelete.Collection(navAfter.Metadata.Name).CurrentValue ?? Enumerable.Empty<object>())
                   .Cast<object>();
    foreach (var child in children)
        db.Entry(child).Property(fkName).CurrentValue = rootEntryAfterDelete.Property(rootKeyName).CurrentValue;
}

            foreach (var entry in WalkGraph(db, root))
            {
                var key = entry.Metadata.FindPrimaryKey()!;
                bool isDefault = key.Properties.All(p =>
                {
                    var v = entry.Property(p.Name).CurrentValue;
                    return IsDefaultValue(v, p.ClrType);
                });

                if (isDefault)
                {
                    entry.State = EntityState.Added;
                    continue;
                }

                bool exists = await KeyExistsAsync(db, entry.Entity, key, ct);
                entry.State = exists ? EntityState.Modified : EntityState.Added;
            }

            await db.SaveChangesAsync(ct);
        }

        // --------------------------------------------------------------------
        //  Graph traversal that never attaches duplicates
        // --------------------------------------------------------------------
        public static IEnumerable<EntityEntry> WalkGraph(DbContext db, object root)
        {
            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            var stack = new Stack<object>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (current == null || !visited.Add(current)) continue;

                var entry = db.Entry(current);
                yield return entry;

                foreach (var nav in entry.Navigations)
                {
                    var val = nav.CurrentValue;
                    if (val == null) continue;

                    if (nav.Metadata.IsCollection)
                    {
                        foreach (var child in (IEnumerable)val)
                            stack.Push(child);
                    }
                    else
                    {
                        stack.Push(val);
                    }
                }
            }
        }

        // --------------------------------------------------------------------
        //  Existence check: fast Any() with composite-PK support
        // --------------------------------------------------------------------
        public static async Task<bool> KeyExistsAsync(
            DbContext db, object entity, IKey key, CancellationToken ct)
        {
            var entityType = db.Model.FindEntityType(entity.GetType())!;
            var primaryKey = entityType.FindPrimaryKey()!;
            var keyValues = primaryKey.Properties
                .Select(p => entityType
                    .FindProperty(p.Name)!
                    .GetGetter()
                    .GetClrValue(entity))
                .ToArray();

            var existingEntity = await db.FindAsync(entity.GetType(), keyValues, ct);
            var exists = existingEntity != null;
            if (existingEntity != null)
                db.Entry(existingEntity).State = EntityState.Detached;

            return exists;
        }

        public static IQueryable GetQueryable(DbContext db, Type t)
        {
            var setMethod = typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!
                                             .MakeGenericMethod(t);
            return (IQueryable)setMethod.Invoke(db, null)!;
        }

        public static bool IsDefaultValue(object? value, Type type)
        {
            if (value == null) return true;           // reference-type default

            if (type == typeof(string))
                return string.IsNullOrEmpty((string)value);

            if (type.IsValueType)
            {
                object defaultVal = Activator.CreateInstance(type)!; // safe for value types
                return value.Equals(defaultVal);
            }

            return false; // non-null reference type
        }
    }
}
