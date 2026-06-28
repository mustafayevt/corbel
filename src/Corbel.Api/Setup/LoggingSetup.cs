using System.Globalization;
using Serilog;

namespace Corbel.Setup;

/// <summary>
/// Serilog as the single logging pipeline: a readable console sink for local dev, plus OTLP export to the
/// Aspire dashboard / a collector when <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> is set. Levels are config-driven
/// (Serilog section in appsettings). Traces and metrics still flow through Aspire's OpenTelemetry setup.
/// </summary>
public static class LoggingSetup
{
    public static WebApplicationBuilder AddSerilogLogging(this WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();

        builder.Services.AddSerilog((services, loggerConfiguration) =>
        {
            loggerConfiguration
                .ReadFrom.Configuration(builder.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture);

            var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                loggerConfiguration.WriteTo.OpenTelemetry(options => options.Endpoint = otlpEndpoint);
        });

        return builder;
    }
}
