using System.Linq.Expressions;
using Corbel.Domain.Common;
using Corbel.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Corbel.Infrastructure.Persistence;

/// <summary>
/// EF Core context backed by PostgreSQL. Extends ASP.NET Core Identity (users, roles, claims) and adds the
/// application aggregates. A single named "SoftDelete" query filter is applied by reflection to every
/// <see cref="ISoftDelete"/> entity so deleted rows disappear from normal queries; opt a query back in with
/// <c>IgnoreQueryFilters(["SoftDelete"])</c> rather than dropping every filter on the query.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    /// <summary>The query-filter name shared by all soft-deletable entities; pass to IgnoreQueryFilters.</summary>
    public const string SoftDeleteFilter = "SoftDelete";

    public DbSet<Note> Notes => Set<Note>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        ApplySoftDeleteQueryFilters(builder);
    }

    /// <summary>
    /// Adds a named <c>e =&gt; !e.IsDeleted</c> filter to every mapped entity implementing
    /// <see cref="ISoftDelete"/>, built once via expression trees so individual configurations never have to
    /// remember it. NOTE: a soft-deleted row still occupies any UNIQUE index, so if you make a uniquely-indexed
    /// entity soft-deletable, scope that index with a matching partial filter (e.g.
    /// <c>HasIndex(...).IsUnique().HasFilter("is_deleted = false")</c>) or a new row can't reuse the deleted
    /// key. (No live entity hits this today — Note is the only soft-deletable type and has no unique index.)
    /// </summary>
    private static void ApplySoftDeleteQueryFilters(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
                continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var isDeleted = Expression.Property(parameter, nameof(ISoftDelete.IsDeleted));
            var lambda = Expression.Lambda(Expression.Not(isDeleted), parameter);

            builder.Entity(entityType.ClrType).HasQueryFilter(SoftDeleteFilter, lambda);
        }
    }
}
