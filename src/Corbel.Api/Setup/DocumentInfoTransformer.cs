using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Corbel.Setup;

/// <summary>
/// Fills in the human-facing top matter of the OpenAPI document that .NET can't infer: a real title, a
/// markdown overview (auth transports, the error model, rate limits, pagination), contact + license, and a
/// description on each tag group. This is what turns the Scalar UI / a generated SDK from "a list of routes"
/// into something a consumer can actually read.
/// </summary>
internal sealed class DocumentInfoTransformer : IOpenApiDocumentTransformer
{
    private const string Description =
        """
        The HTTP contract for the **Corbel** API — a .NET 10 vertical-slice starter.

        ## Authentication
        Two interchangeable transports back the same JWT auth:
        - **Cookie mode** (browser default, `useCookies: true` on login): the access token is returned in the body,
          while the refresh token is set as an httpOnly, path-scoped cookie alongside a signed double-submit CSRF
          token. Cookie-mode `refresh` and `logout` require that CSRF token.
        - **Bearer mode** (native/mobile, `useCookies: false`): the refresh token is returned in the body; send the
          access token as `Authorization: Bearer <token>`.

        Access tokens are short-lived — rotate them at `POST /api/auth/refresh`. Replaying an already-rotated
        refresh token revokes the entire token family (reuse detection).

        ## Errors
        Every error is an [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) ProblemDetails carrying a stable,
        machine-readable `errorCode` (see the enum on the `ProblemDetails` schema) and a correlation `traceId`.
        Validation failures (`400`) additionally include an `errors` map of field name → messages.

        ## Rate limiting
        All endpoints are rate limited — 100 requests/minute per user globally, and a tighter 10/minute per IP on
        the auth endpoints. A rejected request returns `429` with a `Retry-After` header.

        ## Pagination
        List endpoints return a paged envelope (`items` plus `page`, `pageSize`, `totalCount`, `totalPages`,
        `hasNext`, `hasPrevious`). `page` defaults to 1; `pageSize` defaults to 20 and is clamped to 1–100.
        """;

    private static readonly Dictionary<string, string> TagDescriptions = new(StringComparer.Ordinal)
    {
        ["Auth"] = "Registration, sign-in, token refresh, sign-out, the current-user profile, and password changes. Supports both the cookie (browser) and bearer (native) transports.",
        ["Notes"] = "CRUD for the authenticated user's notes — the reference vertical slice. Every note is owned by its creator; a note you don't own is indistinguishable from one that doesn't exist (404).",
        ["Admin"] = "Endpoints gated by the Admin role.",
    };

    public Task TransformAsync(
        OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info.Title = "Corbel API";
        document.Info.Description = Description;
        document.Info.Contact = new OpenApiContact
        {
            Name = "Corbel",
            // your-org is a template placeholder — the README "make it your own" checklist tracks replacing it.
            Url = new Uri("https://github.com/your-org/corbel"),
        };
        document.Info.License = new OpenApiLicense
        {
            Name = "MIT",
            Url = new Uri("https://opensource.org/license/mit"),
        };

        // The framework pre-creates a tag entry per WithTags(...) name; attach a description to each.
        if (document.Tags is { } tags)
            foreach (var tag in tags)
                if (tag.Name is { } name && TagDescriptions.TryGetValue(name, out var description))
                    tag.Description = description;

        return Task.CompletedTask;
    }
}
