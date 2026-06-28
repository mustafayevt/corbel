using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Corbel.Setup;

/// <summary>
/// Registers the API's two auth transports as OpenAPI security schemes and applies the bearer requirement to
/// every operation that actually needs authentication. <b>Bearer</b> (a JWT in the Authorization header) is the
/// native/mobile transport; operations that opt out with <c>.AllowAnonymous()</c> (login, register, refresh,
/// logout) are left unsecured so the published contract doesn't falsely claim they need a token. <b>RefreshCookie</b>
/// documents the browser (cookie) transport — the httpOnly refresh cookie plus the double-submit CSRF header — so
/// a consumer can discover it from the spec; it's intentionally not attached as an operation requirement because
/// those endpoints accept either transport and are anonymous. Targets the Microsoft.OpenApi v2 API shipped with
/// .NET 10.
/// </summary>
internal sealed class AuthSecuritySchemesTransformer : IOpenApiDocumentTransformer
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

        document.Components.SecuritySchemes["RefreshCookie"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Cookie,
            Name = "corbel_rt",
            Description =
                "Cookie-mode (browser) auth. Logging in with `useCookies: true` sets the refresh token as the " +
                "httpOnly `corbel_rt` cookie, scoped to `/api/auth`. `POST /api/auth/refresh` and " +
                "`POST /api/auth/logout` read it automatically and **also** require a signed double-submit CSRF " +
                "token: the readable `XSRF-TOKEN` cookie echoed back in the `X-XSRF-TOKEN` request header. " +
                "(Cookie names shown are the defaults; configurable via the `CookieAuth` options.)",
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
