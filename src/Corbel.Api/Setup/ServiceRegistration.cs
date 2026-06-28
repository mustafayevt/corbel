using System.Text.Json;
using System.Text.Json.Serialization;
using Corbel.Common.Behaviors;
using Corbel.Infrastructure.Auth;
using Corbel.Infrastructure.Persistence;
using FluentValidation;
using Mediator;

namespace Corbel.Setup;

/// <summary>Composition root. Each area exposes one Add* extension; this wires them plus a few cross-cutting services.</summary>
public static class ServiceRegistration
{
    public static WebApplicationBuilder AddApplicationServices(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var configuration = builder.Configuration;

        // Cap the request body for this JSON API (Kestrel's default is 30 MB). A slice that genuinely needs more
        // (e.g. an upload) overrides this per-endpoint with .WithMetadata(new RequestSizeLimitAttribute(...)).
        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 1 * 1024 * 1024);

        // Infrastructure + feature areas (each owns its registration).
        services.AddPersistence(configuration);
        services.AddAuth();

        // Mediator — Scoped is mandatory: the default Singleton captures scoped AppDbContext/validators and throws
        // "cannot consume scoped service from singleton" on the first request.
        services.AddMediator(options => options.ServiceLifetime = ServiceLifetime.Scoped);
        services.AddValidatorsFromAssembly(typeof(Program).Assembly, includeInternalTypes: true);

        // Pipeline behaviors (martinothamar/Mediator does NOT auto-register them). Order = execution order:
        // validation runs first (reject bad input before opening a transaction), then the command transaction.
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

        // JSON contract: enums as strings + camelCase, so the generated TS client gets string unions, not ints.
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        services.AddApiProblemDetails();

        // Readiness probe — a DB-connectivity check tagged "ready" (ServiceDefaults maps /health/ready to it). The
        // 2s timeout keeps the probe responsive instead of hanging on Npgsql's connect timeout.
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"], timeout: TimeSpan.FromSeconds(2));

        services.AddApiOpenApi();
        services.AddApiHardening(configuration);
        services.AddApiRateLimiting();

        // Migrations (config-gated) + seeding at startup — via a hosted service so it never runs during a build.
        services.AddHostedService<DatabaseInitializer>();

        return builder;
    }
}
