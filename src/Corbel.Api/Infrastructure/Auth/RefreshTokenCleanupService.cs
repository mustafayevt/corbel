using Corbel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Corbel.Infrastructure.Auth;

/// <summary>
/// Periodically deletes refresh-token rows that are past their absolute expiry, so the table doesn't grow without
/// bound as logins and rotations accumulate dead rows. Absolutely-expired tokens are already rejected by
/// <see cref="RefreshTokenService.RotateAsync"/>, so they serve no purpose for reuse detection or the grace window
/// and are safe to remove; consumed-but-still-within-window rows are deliberately kept (reuse detection reads
/// them). Runs daily, on a <see cref="TimeProvider"/>-driven timer. A serious deployment may prefer an external
/// scheduled job — if so, delete this service and run the equivalent <c>DELETE</c> on a cron instead.
/// </summary>
public sealed class RefreshTokenCleanupService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<RefreshTokenCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait one interval before the first sweep so it never races the startup migration in DatabaseInitializer
        // (a day's worth of dead rows is harmless). WaitForNextTickAsync returns false / throws on shutdown.
        using var timer = new PeriodicTimer(Interval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await PurgeExpiredAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A transient purge failure must never tear down the host — log and retry on the next tick.
                logger.LogError(ex, "Refresh-token cleanup failed; will retry on the next interval.");
            }
        }
    }

    private async Task PurgeExpiredAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = timeProvider.GetUtcNow();
        var removed = await db.RefreshTokens
            .Where(token => token.AbsoluteExpiresAtUtc < now)
            .ExecuteDeleteAsync(cancellationToken);

        if (removed > 0)
            logger.LogInformation("Purged {Count} expired refresh token(s).", removed);
    }
}
