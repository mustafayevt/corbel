using Corbel.Common.Messaging;
using Corbel.Common.Web;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Corbel.Features.Admin;

/// <summary>A minimal Admin-only endpoint — the working example that exercises the Admin authorization policy + seeded Admin role.</summary>
public sealed class AdminPingEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("admin/ping", Handle)
            .WithName("AdminPing")
            .WithTags("Admin")
            .RequireAuthorization(AuthorizationPolicies.Admin)
            .WithSummary("Admin-only ping.")
            .WithDescription(
                "Returns a simple acknowledgement. Requires the `Admin` role; an authenticated caller without it gets 403.\n\n"
                + "**Errors:** 401 `common.unauthorized`, 403 `common.forbidden`, 429 `common.rate_limited`.")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

    private static Ok<MessageResponse> Handle() => TypedResults.Ok(new MessageResponse("pong"));
}
