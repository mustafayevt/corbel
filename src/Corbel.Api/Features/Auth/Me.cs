using Corbel.Common.Abstractions;
using Corbel.Common.Exceptions;
using Corbel.Common.Web;
using Corbel.Domain.Entities;
using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace Corbel.Features.Auth;

/// <summary>The current user's profile. In cookie mode the access token isn't JS-readable, so the SPA calls this to hydrate identity/roles on load.</summary>
public sealed record MeQuery : IRequest<UserResponse>;

public sealed class MeHandler(ICurrentUser currentUser, UserManager<AppUser> userManager)
    : IRequestHandler<MeQuery, UserResponse>
{
    public async ValueTask<UserResponse> Handle(MeQuery query, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(currentUser.RequireId().ToString())
                   ?? throw new NotAuthenticatedException();

        var roles = await userManager.GetRolesAsync(user);
        return new UserResponse(user.Id, user.Email!, user.DisplayName, [.. roles]);
    }
}

public sealed class MeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("auth/me", Handle)
            .WithName("Me")
            .WithTags("Auth")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

    private static async Task<Ok<UserResponse>> Handle(ISender sender, CancellationToken cancellationToken)
        => TypedResults.Ok(await sender.Send(new MeQuery(), cancellationToken));
}
