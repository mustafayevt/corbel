using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Corbel.Setup;

/// <summary>
/// Registers the OpenAPI 3.1 document and shapes it into a thoroughly documented contract: the human-facing
/// info/tags top matter (<see cref="DocumentInfoTransformer"/>), the Bearer and cookie auth schemes
/// (<see cref="AuthSecuritySchemesTransformer"/>), the parameter
/// descriptions and request/response examples (<see cref="OperationDocsTransformer"/>), "…Command" request
/// bodies renamed to "…Request", and the per-schema fixes in <see cref="ClientContractSchemaTransformer"/>.
/// Operation summaries and descriptions come from the fluent <c>.WithSummary()</c>/<c>.WithDescription()</c> on
/// each endpoint. Served at runtime (/openapi/v1.json) and turned into the typed TS client by <c>just gen-client</c>.
/// </summary>
internal static class OpenApiSetup
{
    public static IServiceCollection AddApiOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer<DocumentInfoTransformer>();
            options.AddDocumentTransformer<AuthSecuritySchemesTransformer>();
            options.AddSchemaTransformer<ClientContractSchemaTransformer>();
            options.AddOperationTransformer<OperationDocsTransformer>();

            // Stabilize public schema names so the generated TS client stays decoupled from internal types: write
            // commands are bound directly as request bodies, so expose them as "…Request" rather than leaking the
            // internal "…Command" suffix into the public contract.
            options.CreateSchemaReferenceId = jsonTypeInfo =>
            {
                var id = OpenApiOptions.CreateDefaultSchemaReferenceId(jsonTypeInfo);
                return id is not null && id.EndsWith("Command", StringComparison.Ordinal)
                    ? string.Concat(id.AsSpan(0, id.Length - "Command".Length), "Request")
                    : id;
            };
        });

        return services;
    }
}
