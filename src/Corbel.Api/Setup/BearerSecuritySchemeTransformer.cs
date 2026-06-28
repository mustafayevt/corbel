using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Corbel.Setup;

/// <summary>
/// Injects the JWT Bearer security scheme into the OpenAPI document and applies it to every operation that
/// actually requires authentication, so Scalar's "Authorize" works and a generated SDK knows where a token is
/// needed. Operations whose endpoint opts out with <c>.AllowAnonymous()</c> (login, register, refresh, logout)
/// are left unsecured, so the published contract doesn't falsely claim they need a token. Targets the
/// Microsoft.OpenApi v2 API shipped with .NET 10.
/// </summary>
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste a JWT access token (without the 'Bearer ' prefix).",
        };

        var requirement = new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
        };

        // Endpoints that called .AllowAnonymous() (keyed METHOD:/path) must NOT advertise a security requirement.
        var anonymous = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in context.DescriptionGroups)
            foreach (var description in group.Items)
                if (description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
                    anonymous.Add($"{description.HttpMethod}:/{description.RelativePath}");

        foreach (var (pathKey, pathItem) in document.Paths)
            foreach (var (operationType, operation) in pathItem.Operations ?? new Dictionary<HttpMethod, OpenApiOperation>())
                if (!anonymous.Contains($"{operationType.Method}:{pathKey}"))
                    (operation.Security ??= []).Add(requirement);

        return Task.CompletedTask;
    }
}
