using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Corbel.Setup;

/// <summary>
/// Registers the OpenAPI 3.1 document and shapes it for a clean generated TypeScript client: a Bearer scheme
/// (<see cref="BearerSecuritySchemeTransformer"/>), "…Command" request bodies renamed to "…Request", and the
/// per-schema fixes in <see cref="ClientContractSchemaTransformer"/>. Served at runtime (/openapi/v1.json).
/// </summary>
internal static class OpenApiSetup
{
    public static IServiceCollection AddApiOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
            options.AddSchemaTransformer<ClientContractSchemaTransformer>();

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
