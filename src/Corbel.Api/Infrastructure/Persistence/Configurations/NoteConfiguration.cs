using Corbel.Common;
using Corbel.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Corbel.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Note"/>: bounded text columns, a composite index for the per-owner list query, a
/// navigation-less FK to the owning user, and optimistic concurrency via PostgreSQL's system <c>xmin</c> column —
/// a real concurrency token on Npgsql, unlike <c>IsRowVersion()</c>, which silently does nothing there.
/// </summary>
public sealed class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        builder.Property(n => n.Title).HasMaxLength(NoteConstraints.TitleMaxLength);
        builder.Property(n => n.Content).HasMaxLength(NoteConstraints.ContentMaxLength);

        // Backs the list query: WHERE OwnerId = … ORDER BY CreatedAtUtc DESC (and covers the FK below, so no
        // duplicate single-column index is generated for it).
        builder.HasIndex(n => new { n.OwnerId, n.CreatedAtUtc });

        // Navigation-less FK to the owning user — mirrors RefreshToken's reference-by-id so referential integrity
        // is consistent across the user-owned aggregates. Restrict (not Cascade) deliberately: a hard user-delete
        // must not silently bypass Note's soft-delete; deal with a user's notes explicitly before removing them.
        builder.HasOne<AppUser>().WithMany().HasForeignKey(n => n.OwnerId).OnDelete(DeleteBehavior.Restrict);

        // Optimistic concurrency via PostgreSQL's system "xmin" column, mapped as a shadow token. (Npgsql 10
        // has no UseXminAsConcurrencyToken helper, so the shadow property is configured by hand.) Npgsql strips
        // the column at SQL generation — it's never created — so this is a no-op DDL-wise; a stale update raises
        // DbUpdateConcurrencyException → 409.
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();
    }
}
