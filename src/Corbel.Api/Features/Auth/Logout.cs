using Corbel.Common.Exceptions;
using Corbel.Common.Messaging;
using Corbel.Common.Web;
using Corbel.Infrastructure.Auth;
using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Corbel.Features.Auth;

/// <summary>Bearer-mode body; in cookie mode the refresh token is read from the httpOnly cookie.</summary>
/// <param name="RefreshToken">The raw refresh token to revoke (bearer mode only). Leave null in cookie mode.</param>
public sealed record LogoutRequest(string? RefreshToken);

public sealed record LogoutCommand(string? RawToken) : IRequest<MessageResponse>;

public sealed class LogoutHandler(RefreshTokenService refreshTokens) : IRequestHandler<LogoutCommand, MessageResponse>
{
    public async ValueTask<MessageResponse> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        // Revoke the presented token's whole family so its lineage can't be rotated again.
        if (!string.IsNullOrEmpty(command.RawToken))
            await refreshTokens.RevokeFamilyAsync(command.RawToken, cancellationToken);

        return new MessageResponse("You have been signed out.");
    }
}

public sealed class LogoutEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("auth/logout", Handle)
            .WithName("Logout")
            .WithTags("Auth")
            // AllowAnonymous: an already-expired access token must still be able to revoke its refresh family and
            // clear the cookies. The refresh cookie now also rides this path, so in cookie mode the handler
            // enforces the double-submit CSRF check itself (mirroring refresh); bearer mode carries the token in
            // the body and is unaffected.
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .WithSummary("Sign out and revoke the refresh token.")
            .WithDescription(
                "Revokes the presented refresh token's whole family and clears the auth cookies. Allowed anonymously so an expired access token can still sign out; cookie mode requires a valid CSRF token.\n\n"
                + "**Errors:** 403 `common.forbidden` (missing/invalid CSRF), 429 `common.rate_limited`.")
            .ProducesProblem(StatusCodes.Status403Forbidden);

    private static async Task<Ok<MessageResponse>> Handle(
        LogoutRequest? body, ISender sender, HttpContext httpContext, AuthCookies cookies, CancellationToken cancellationToken)
    {
        // Prefer the cookie (browser); fall back to the body (native/bearer client).
        var cookieToken = cookies.ReadRefresh(httpContext);
        var useCookies = !string.IsNullOrEmpty(cookieToken);
        var rawToken = useCookies ? cookieToken : body?.RefreshToken;

        // Cookie mode is CSRF-sensitive (the refresh cookie is sent automatically), so require the double-submit
        // token before revoking the family — same guard the refresh endpoint applies.
        if (useCookies && !cookies.ValidateCsrf(httpContext))
            throw new CsrfValidationException();

        var result = await sender.Send(new LogoutCommand(rawToken), cancellationToken);
        cookies.Clear(httpContext);
        return TypedResults.Ok(result);
    }
}
