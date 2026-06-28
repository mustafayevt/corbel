using Corbel.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Corbel.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="RefreshToken"/>: the SHA-256 hash is the unique lookup key, the family index backs
/// reuse-detection revocation, and a cascading FK to the owning user purges tokens when an account is deleted
/// (the FK convention also indexes <c>UserId</c>).
/// </summary>
public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.Property(t => t.TokenHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.FamilyId);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
