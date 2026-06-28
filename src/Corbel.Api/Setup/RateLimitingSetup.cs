using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Corbel.Common.Web;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Corbel.Setup;

/// <summary>
/// Per-request rate limiting: a global sliding window keyed by the immutable <c>sub</c> claim (IP fallback) and a
/// tighter fixed-window "auth" policy keyed by IP (auth endpoints run before authentication) that those endpoints
/// opt into. A rejected request returns 429 with a <c>Retry-After</c> hint.
/// </summary>
internal static class RateLimitingSetup
{
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Surface a machine-readable backoff hint so well-behaved clients retry sanely instead of hammering.
            options.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(NumberFormatInfo.InvariantInfo);
                return ValueTask.CompletedTask;
            };

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var partitionKey = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub) is { } sub
                    ? $"u:{sub}"
                    : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "anonymous"}";

                return RateLimitPartition.GetSlidingWindowLimiter(partitionKey, _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 6,
                });
            });

            options.AddPolicy(RateLimitPolicies.Auth, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
        });

        return services;
    }
}
