using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Utils
{
    public static class DbContextUpsertExtensions
    {
        /// <summary>
        /// Upserts an entire object graph. New entities (determined by default PK values)
        /// are <see cref="EntityState.Added"/>; existing entities are <see cref="EntityState.Modified"/>.
        /// Any children that were removed from collection navigations are deleted.
        /// </summary>
        public static async Task UpsertGraphAsync<T>(
            this DbContext db,
            T root,
            CancellationToken ct = default) where T : class
        {
            if (root == null) throw new ArgumentNullException(nameof(root));

            // -----------------------------------------------------------------
            // 1. Detect children that were removed from collection navigations
            // -----------------------------------------------------------------
            var rootEntry = db.Entry(root);

            foreach (var nav in rootEntry.Navigations.Where(n => n.Metadata.IsCollection))
            {
                var collection = rootEntry.Collection(nav.Metadata.Name);

                // Current children coming from DB
                var dbChildren = await collection.Query().Cast<object>().ToListAsync(ct);

                // Children present in the new graph
                var graphChildren = ((IEnumerable)collection.CurrentValue ?? Enumerable.Empty<object>())
                                    .Cast<object>();

                var graphKeys = new HashSet<object>(
                    graphChildren.Select(c => db.Entry(c).Property("Id").CurrentValue),
                    ReferenceEqualityComparer.Instance);

                foreach (var child in dbChildren)
                {
                    var key = db.Entry(child).Property("Id").CurrentValue;
                    if (!graphKeys.Contains(key))
                    {
                        db.Remove(child); // mark missing child for deletion
                    }
                }
            }

            // -----------------------------------------------------------------
            // 2. Traverse the graph and set Added / Modified appropriately
            // -----------------------------------------------------------------
            foreach (var entry in WalkGraph(db, root))
            {
                var pk = entry.Metadata.FindPrimaryKey()!;

                bool isTransient = pk.Properties.All(p =>
                {
                    var v = entry.Property(p.Name).CurrentValue;
                    return IsDefaultValue(v, p.ClrType);
                });

                if (isTransient)
                {
                    entry.State = EntityState.Added;
                    continue;
                }

                entry.State = await KeyExistsAsync(db, entry.Entity, pk, ct)
                    ? EntityState.Modified
                    : EntityState.Added;
            }

            // -----------------------------------------------------------------
            // 3. Persist everything in a single round-trip
            // -----------------------------------------------------------------
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
        //  Efficient existence check (supports composite PKs)
        // --------------------------------------------------------------------
        private static async Task<bool> KeyExistsAsync(
            DbContext db, object entity, IKey key, CancellationToken ct)
        {
            var entityType = db.Model.FindEntityType(entity.GetType())!;
            var pk = entityType.FindPrimaryKey()!;

            var keyValues = pk.Properties
                .Select(p => entityType.FindProperty(p.Name)!.GetGetter().GetClrValue(entity))
                .ToArray();

            var tracked = await db.FindAsync(entity.GetType(), keyValues, ct);
            if (tracked != null)
            {
                // Prevent duplicate tracking of the existing row
                db.Entry(tracked).State = EntityState.Detached;
            }

            return tracked != null;
        }

        // --------------------------------------------------------------------
        //  Helpers
        // --------------------------------------------------------------------
        private static bool IsDefaultValue(object? value, Type type)
        {
            if (value == null) return true; // reference-type default

            if (type == typeof(string))
                return string.IsNullOrEmpty((string)value);

            if (type.IsValueType)
            {
                object defaultVal = Activator.CreateInstance(type)!; // safe for structs
                return value.Equals(defaultVal);
            }

            return false; // non-null reference type
        }
    }
}
