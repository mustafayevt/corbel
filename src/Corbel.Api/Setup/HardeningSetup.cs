using Corbel.Common.Web;
using Microsoft.AspNetCore.HttpOverrides;

namespace Corbel.Setup;

/// <summary>
/// Edge/transport hardening for direct-to-backend access: an HSTS fallback (nginx owns the edge header in the
/// reference deployment), trust of the same-origin proxy's <c>X-Forwarded-*</c> headers, and a CORS policy for
/// genuinely split-origin / mobile clients (same-origin requests go through nginx and need none).
/// </summary>
internal static class HardeningSetup
{
    public static IServiceCollection AddApiHardening(this IServiceCollection services, IConfiguration configuration)
    {
        // HSTS policy used by UseHsts() in non-Development: a 1-year max-age with includeSubDomains. preload is
        // deliberately left off — it is an irreversible commitment that belongs to the site owner, not a template.
        services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(365);
            options.IncludeSubDomains = true;
        });

        // Forwarded headers — TLS is terminated at nginx. Clearing both known-lists trusts the X-Forwarded-*
        // headers unconditionally, which is correct behind the same-origin compose proxy (the API is published
        // ONLY to nginx, never directly). SECURITY: if you ever expose the API directly, set KnownProxies/
        // KnownNetworks to your real proxy — otherwise a client can spoof X-Forwarded-For to forge its source IP,
        // defeating the per-IP auth rate limit and IP-based brute-force/lockout pressure.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        // CORS — only relevant for genuinely split-origin / mobile clients (same-origin goes through nginx).
        var corsOrigins = configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
        services.AddCors(options => options.AddPolicy(CorsPolicies.Spa, policy =>
        {
            if (corsOrigins.Length > 0)
                policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        }));

        return services;
    }
}
