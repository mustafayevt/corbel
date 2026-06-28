using Corbel.Domain.Entities;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;

namespace Corbel.Api.Tests.Unit;

/// <summary>
/// Rotation/expiry rules of the <see cref="RefreshToken"/> lineage. Time is driven by a
/// <see cref="FakeTimeProvider"/> so the absolute-cap and sliding-window math is fully deterministic.
/// </summary>
public sealed class RefreshTokenTests
{
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IssueNewFamily_starts_active_and_self_rooted()
    {
        var time = new FakeTimeProvider(Start);

        var token = RefreshToken.IssueNewFamily(UserId, "hash-1", time.GetUtcNow(), slidingDays: 7, absoluteDays: 30);

        token.FamilyId.ShouldBe(token.Id); // a new lineage is rooted at itself
        token.UserId.ShouldBe(UserId);
        token.ConsumedAtUtc.ShouldBeNull();
        token.RevokedAtUtc.ShouldBeNull();
        token.IsActive(time.GetUtcNow()).ShouldBeTrue();
    }

    [Fact]
    public void Rotate_consumes_parent_and_issues_active_child_in_same_family()
    {
        var time = new FakeTimeProvider(Start);
        var parent = RefreshToken.IssueNewFamily(UserId, "hash-1", time.GetUtcNow(), slidingDays: 7, absoluteDays: 30);

        time.Advance(TimeSpan.FromDays(1));
        var child = parent.Rotate("hash-2", time.GetUtcNow(), slidingDays: 7);

        // Parent is consumed and points at its replacement.
        parent.ConsumedAtUtc.ShouldBe(time.GetUtcNow());
        parent.ReplacedByTokenId.ShouldBe(child.Id);
        parent.IsActive(time.GetUtcNow()).ShouldBeFalse();

        // Child continues the same lineage and is active.
        child.Id.ShouldNotBe(parent.Id);
        child.FamilyId.ShouldBe(parent.FamilyId);
        child.IsActive(time.GetUtcNow()).ShouldBeTrue();
    }

    [Fact]
    public void Consumed_token_is_inactive_so_reuse_is_detectable()
    {
        var time = new FakeTimeProvider(Start);
        var parent = RefreshToken.IssueNewFamily(UserId, "hash-1", time.GetUtcNow(), slidingDays: 7, absoluteDays: 30);

        parent.Rotate("hash-2", time.GetUtcNow(), slidingDays: 7);

        // Presenting the already-consumed parent again: ConsumedAtUtc is set and it is not active, which is
        // exactly the signal the token store uses to detect reuse and revoke the whole family.
        parent.ConsumedAtUtc.ShouldNotBeNull();
        parent.IsActive(time.GetUtcNow()).ShouldBeFalse();
    }

    [Fact]
    public void Absolute_cap_expires_token_even_within_the_sliding_window()
    {
        var time = new FakeTimeProvider(Start);
        var token = RefreshToken.IssueNewFamily(UserId, "hash-1", time.GetUtcNow(), slidingDays: 7, absoluteDays: 30);

        // Rotate late in the lifetime: the child's sliding window reaches t+35d, but the absolute cap is
        // preserved at the original t+30d.
        time.Advance(TimeSpan.FromDays(28));
        var child = token.Rotate("hash-2", time.GetUtcNow(), slidingDays: 7);

        child.ExpiresAtUtc.ShouldBe(Start.AddDays(35));
        child.AbsoluteExpiresAtUtc.ShouldBe(Start.AddDays(30));

        child.IsActive(Start.AddDays(29)).ShouldBeTrue();  // inside both windows
        child.IsActive(Start.AddDays(31)).ShouldBeFalse(); // inside sliding (t+35d) but past the absolute cap (t+30d)
    }
}
