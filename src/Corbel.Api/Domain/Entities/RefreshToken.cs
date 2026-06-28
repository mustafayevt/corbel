using Corbel.Domain.Common;

namespace Corbel.Domain.Entities;

/// <summary>
/// A single-use refresh token, stored only as a SHA-256 hash. Rotation creates a child in the same
/// <see cref="FamilyId"/> lineage; presenting an already-consumed token lets the store revoke the whole
/// family (reuse detection). <see cref="AbsoluteExpiresAtUtc"/> caps the sliding lifetime.
/// </summary>
public sealed class RefreshToken : Entity
{
    private RefreshToken() { } // EF

    private RefreshToken(
        Guid id, Guid userId, string tokenHash, Guid familyId,
        DateTimeOffset createdAtUtc, DateTimeOffset expiresAtUtc, DateTimeOffset absoluteExpiresAtUtc)
        : base(id)
    {
        UserId = userId;
        TokenHash = tokenHash;
        FamilyId = familyId;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        AbsoluteExpiresAtUtc = absoluteExpiresAtUtc;
    }

    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = default!;
    public Guid FamilyId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset AbsoluteExpiresAtUtc { get; private set; }
    public DateTimeOffset? ConsumedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }

    public bool IsActive(DateTimeOffset now)
        => ConsumedAtUtc is null && RevokedAtUtc is null && now < ExpiresAtUtc && now < AbsoluteExpiresAtUtc;

    /// <summary>Starts a brand-new lineage (its family id equals its own id).</summary>
    public static RefreshToken IssueNewFamily(
        Guid userId, string tokenHash, DateTimeOffset now, int slidingDays, int absoluteDays)
    {
        var id = Guid.CreateVersion7();
        return new RefreshToken(
            id, userId, tokenHash, familyId: id,
            now, now.AddDays(slidingDays), now.AddDays(absoluteDays));
    }

    /// <summary>Consumes this token and returns its replacement in the same family, preserving the absolute cap.</summary>
    public RefreshToken Rotate(string newTokenHash, DateTimeOffset now, int slidingDays)
    {
        var child = new RefreshToken(
            Guid.CreateVersion7(), UserId, newTokenHash, FamilyId,
            now, now.AddDays(slidingDays), AbsoluteExpiresAtUtc);
        ConsumedAtUtc = now;
        ReplacedByTokenId = child.Id;
        return child;
    }
}
