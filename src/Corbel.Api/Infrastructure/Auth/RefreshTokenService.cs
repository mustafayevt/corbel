using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Corbel.Common.Exceptions;
using Corbel.Common.Options;
using Corbel.Domain.Entities;
using Corbel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Corbel.Infrastructure.Auth;

/// <summary>The userId carried back from a successful rotation so the caller can mint a fresh access token.</summary>
public sealed record RefreshResult(string RawToken, Guid UserId);

/// <summary>
/// Opaque, single-use refresh tokens with rotation and reuse detection. The raw token (32 bytes of CSPRNG,
/// base64url) is returned to the client once; only its SHA-256 hash is stored. Each rotation consumes the
/// presented token and issues a child in the same <see cref="RefreshToken.FamilyId"/> lineage. Presenting an
/// already-consumed token outside a short grace window revokes the whole family (reuse detection). The consume
/// is gated by a conditional UPDATE (rows-affected), so concurrent rotations cannot both win.
/// </summary>
public sealed class RefreshTokenService(AppDbContext db, TimeProvider timeProvider, IOptions<JwtOptions> jwtOptions)
{
    // A just-rotated token may legitimately be presented again (a retried/double-fired request whose response
    // was lost). Within this window we rotate its child instead of treating it as an attack.
    private static readonly TimeSpan ReuseGraceWindow = TimeSpan.FromSeconds(10);

    private readonly JwtOptions _options = jwtOptions.Value;

    /// <summary>Starts a brand-new token family for the user and returns the raw token.</summary>
    public async Task<string> IssueAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var raw = GenerateRawToken();
        var token = RefreshToken.IssueNewFamily(
            userId, Hash(raw), timeProvider.GetUtcNow(), _options.RefreshTokenDays, _options.RefreshAbsoluteDays);

        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync(cancellationToken);
        return raw;
    }

    /// <summary>Validates and rotates a refresh token, returning its replacement. Throws on invalid/expired tokens and on detected reuse.</summary>
    public async Task<RefreshResult> RotateAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var hash = Hash(rawToken);

        var token = await db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (token is null)
            throw new InvalidRefreshTokenException();

        if (token.RevokedAtUtc is not null || now >= token.AbsoluteExpiresAtUtc)
            throw new InvalidRefreshTokenException();

        if (token.ConsumedAtUtc is not null)
            return await HandleConsumedAsync(token, now, cancellationToken);

        if (now >= token.ExpiresAtUtc)
            throw new InvalidRefreshTokenException();

        var result = await TryRotateAsync(token, now, cancellationToken);
        if (result is not null)
            return result;

        // Lost the atomic consume race: the token was consumed between our read and our update. Re-read and
        // treat the now-consumed token through the same grace/reuse path.
        var reread = await db.RefreshTokens.AsNoTracking().FirstAsync(t => t.Id == token.Id, cancellationToken);
        return await HandleConsumedAsync(reread, now, cancellationToken);
    }

    /// <summary>Revokes the whole family of the token presented (used on logout).</summary>
    public async Task RevokeFamilyAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        var hash = Hash(rawToken);
        var token = await db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (token is not null)
            await RevokeFamilyInternalAsync(token.FamilyId, timeProvider.GetUtcNow(), cancellationToken);
    }

    /// <summary>Revokes every active refresh token for a user (used on password change/reset).</summary>
    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, timeProvider.GetUtcNow()), cancellationToken);

    private async Task<RefreshResult> HandleConsumedAsync(RefreshToken consumed, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // Grace window: a just-rotated token presented again rotates its IMMEDIATE child instead of nuking the
        // family — this absorbs the common double-fired refresh (a lost response makes the same token fire
        // ~twice). It's deliberately narrow: only the direct child rotates. If that child is itself already
        // consumed (the lineage advanced 2+ steps), the replay is treated as genuine reuse and the family is
        // revoked — a token whose successor already rotated forward is exactly the stolen-token signal reuse
        // detection exists to catch, and the rare cost (a simultaneous retry storm forcing a re-login) fails safe.
        if (consumed.RevokedAtUtc is null &&
            consumed.ConsumedAtUtc is { } consumedAt &&
            now - consumedAt <= ReuseGraceWindow &&
            consumed.ReplacedByTokenId is { } childId)
        {
            var child = await db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(t => t.Id == childId, cancellationToken);
            if (child is not null && child.IsActive(now))
            {
                var grace = await TryRotateAsync(child, now, cancellationToken);
                if (grace is not null)
                    return grace;
            }
        }

        // Genuine reuse: a consumed (or already-revoked) token replayed outside the grace window → burn it all.
        await RevokeFamilyInternalAsync(consumed.FamilyId, now, cancellationToken);
        throw new TokenReuseException();
    }

    /// <summary>
    /// Atomically consumes <paramref name="token"/> and persists its replacement. The conditional UPDATE
    /// (consume only while still unconsumed and unrevoked) returns 0 rows if another request won the race, in
    /// which case this returns null. The parent is consumed before the child is inserted, so a crash mid-way
    /// fails closed (a dead token) rather than minting an unowned one.
    /// </summary>
    private async Task<RefreshResult?> TryRotateAsync(RefreshToken token, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var raw = GenerateRawToken();
        var child = token.Rotate(Hash(raw), now, _options.RefreshTokenDays);

        var consumed = await db.RefreshTokens
            .Where(t => t.Id == token.Id && t.ConsumedAtUtc == null && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(t => t.ConsumedAtUtc, now)
                    .SetProperty(t => t.ReplacedByTokenId, child.Id),
                cancellationToken);

        if (consumed == 0)
            return null;

        db.RefreshTokens.Add(child);
        await db.SaveChangesAsync(cancellationToken);
        return new RefreshResult(raw, child.UserId);
    }

    private async Task RevokeFamilyInternalAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
        => await db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), cancellationToken);

    private static string GenerateRawToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    private static string Hash(string rawToken)
        => Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
