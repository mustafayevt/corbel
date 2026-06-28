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
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

    private static Ok<AdminPingResponse> Handle() => TypedResults.Ok(new AdminPingResponse("pong"));
}

public sealed record AdminPingResponse(string Message);
