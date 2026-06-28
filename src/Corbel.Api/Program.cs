using Corbel.Setup;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();        // Aspire: OpenTelemetry (traces + metrics) + health + outbound HttpClient resilience
builder.AddSerilogLogging();         // Serilog: readable console + OTLP logs
builder.AddApplicationServices();    // DI composition (see Setup/ServiceRegistration)

var app = builder.Build();

app.UseApplicationPipeline();   // middleware order + endpoint mapping (DB init runs in a hosted service)

app.Run();

/// <summary>Exposed as a partial class so integration tests can use <c>WebApplicationFactory&lt;Program&gt;</c>.</summary>
public partial class Program;
