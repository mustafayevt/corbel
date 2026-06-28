using Corbel.Common.Web;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

namespace Corbel.Setup;

/// <summary>Builds the HTTP pipeline (order matters) and maps all endpoints. DB init runs in <see cref="DatabaseInitializer"/>.</summary>
public static class PipelineSetup
{
    public static WebApplication UseApplicationPipeline(this WebApplication app)
    {
        app.UseForwardedHeaders();

        app.UseSerilogRequestLogging(options =>
            // One concise structured line per request — but demote health-probe noise to Verbose so the
            // orchestrator's liveness/readiness polling doesn't flood Information logs (and OTLP cost).
            options.GetLevel = (httpContext, _, exception) => exception is not null || httpContext.Response.StatusCode >= 500
                ? LogEventLevel.Error
                : httpContext.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
                    ? LogEventLevel.Verbose
                    : LogEventLevel.Information);

        app.UseExceptionHandler();      // → GlobalExceptionHandler → ProblemDetails
        app.UseStatusCodePages();

        // HSTS: in the reference deployment nginx terminates TLS and owns the edge Strict-Transport-Security
        // header (see web/nginx.conf). This is a defense-in-depth fallback for any direct-to-backend access; the
        // policy (1-year max-age, includeSubDomains, no preload) is configured in AddApplicationServices.
        if (!app.Environment.IsDevelopment())
            app.UseHsts();
        app.UseHttpsRedirection();

        // Security headers (CSP etc.) in non-Development only. The strict API CSP is right for production but
        // blocks the dev-only Scalar UI's bundled scripts (Scalar is served by this same app), and a JSON API
        // behind a same-origin SPA doesn't need it in the dev loop. Production (behind nginx) keeps them.
        if (!app.Environment.IsDevelopment())
            app.UseSecurityHeaders(policies => policies.AddDefaultApiSecurityHeaders());

        app.UseCors(CorsPolicies.Spa);

        app.UseAuthentication();
        app.UseRateLimiter();   // after authentication so per-user partitions can read HttpContext.User
        app.UseAuthorization();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous();            // /openapi/v1.json
            app.MapScalarApiReference().AllowAnonymous(); // /scalar (fetches the spec from the browser)
        }

        app.MapDefaultEndpoints();            // /health/live, /health/ready (ServiceDefaults)
        app.MapEndpoints();                   // all IEndpoint slices

        return app;
    }
}
