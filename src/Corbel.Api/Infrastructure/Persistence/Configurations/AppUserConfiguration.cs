using Corbel.Common;
using Corbel.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Corbel.Infrastructure.Persistence.Configurations;

/// <summary>
/// Bounds the optional <see cref="AppUser.DisplayName"/> column to the registration input contract, and makes the
/// email index unique so the database enforces RequireUniqueEmail (Identity's default index is non-unique).
/// </summary>
public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.Property(u => u.DisplayName).HasMaxLength(AppUserConstraints.DisplayNameMaxLength);

        // Identity's own email index is non-unique and its app-level uniqueness check isn't race-safe, so make
        // that index unique (matched by property, keeping Identity's "EmailIndex" name): concurrent registrations
        // can't both insert the same address. (Postgres treats NULLs as distinct, so users without an email are
        // unaffected.)
        builder.HasIndex(u => u.NormalizedEmail).IsUnique();
    }
}
