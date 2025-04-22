using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Collections;
using System.Linq.Expressions;

namespace Common.Utils
{
    public static class DbContextUpsertExtensions
    {
        public static async Task UpsertGraphAsync<T>(
            this DbContext db, T root, CancellationToken ct = default)
            where T : class
        {
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
        private static IEnumerable<EntityEntry> WalkGraph(DbContext db, object root)
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
        //  Existence check: fast Any() with composite‑PK support
        // --------------------------------------------------------------------
        private static async Task<bool> KeyExistsAsync(
            DbContext db, object entity, IKey key, CancellationToken ct)
        {
            var clrType = entity.GetType();
            IQueryable set = GetQueryable(db, clrType);   // reflection-safe Set()

            var param = Expression.Parameter(clrType, "e");
            Expression? body = null;

            foreach (var p in key.Properties)
            {
                var left = Expression.Property(param, p.Name);
                var value = clrType.GetProperty(p.Name)!.GetValue(entity);
                var right = Expression.Convert(Expression.Constant(value), p.ClrType);
                var equal = Expression.Equal(left, right);
                body = body == null ? equal : Expression.AndAlso(body, equal);
            }

            var lambda = Expression.Lambda(body!, param);
            var any = typeof(Queryable).GetMethods()
                            .First(m => m.Name == "Any" && m.GetParameters().Length == 2)
                            .MakeGenericMethod(clrType);

            return (bool)any.Invoke(null, new object[] { set, lambda })!;
        }

        private static IQueryable GetQueryable(DbContext db, Type t)
        {
            var setMethod = typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!
                                             .MakeGenericMethod(t);
            return (IQueryable)setMethod.Invoke(db, null)!;
        }

        private static bool IsDefaultValue(object? value, Type type)
        {
            if (value == null) return true;           // reference‑type default

            if (type == typeof(string))
                return string.IsNullOrEmpty((string)value);

            if (type.IsValueType)
            {
                object defaultVal = Activator.CreateInstance(type)!; // safe for value types
                return value.Equals(defaultVal);
            }

            return false; // non‑null reference type
        }
    }
}
