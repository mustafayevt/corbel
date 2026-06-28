using Corbel.Common.Exceptions;
using Corbel.Common.Web;
using Corbel.Domain.Entities;
using Corbel.Infrastructure.Auth;
using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace Corbel.Features.Auth;

/// <summary>Bearer-mode request body. In cookie mode the token is read from the httpOnly cookie instead.</summary>
public sealed record RefreshRequest(string? RefreshToken);

// Plain IRequest (not a transaction-wrapped command): reuse detection revokes the token family and then
// throws 401 — that revocation must persist, so this slice manages its own writes.
public sealed record RefreshCommand(string RawToken) : IRequest<TokenResponse>;

public sealed class RefreshHandler(UserManager<AppUser> userManager, JwtTokenService jwtTokens, RefreshTokenService refreshTokens)
    : IRequestHandler<RefreshCommand, TokenResponse>
{
    public async ValueTask<TokenResponse> Handle(RefreshCommand command, CancellationToken cancellationToken)
    {
        var rotation = await refreshTokens.RotateAsync(command.RawToken, cancellationToken);

        var user = await userManager.FindByIdAsync(rotation.UserId.ToString())
                   ?? throw new InvalidRefreshTokenException();

        // Re-check account state on every refresh: a lockout applied after the original login must cut the session
        // short instead of letting it keep minting access tokens for the rest of the refresh window. Revoke every
        // token for the user so no family survives the lockout, then fail closed — mirrors Login's lockout guard.
        if (await userManager.IsLockedOutAsync(user))
        {
            await refreshTokens.RevokeAllForUserAsync(user.Id, cancellationToken);
            throw new AccountLockedException();
        }

        var roles = await userManager.GetRolesAsync(user);
        var (accessToken, expiresIn) = jwtTokens.CreateAccessToken(user, roles);

        return new TokenResponse(accessToken, expiresIn, rotation.RawToken);
    }
}

public sealed class RefreshEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("auth/refresh", Handle)
            .WithName("Refresh")
            .WithTags("Auth")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

    private static async Task<Ok<TokenResponse>> Handle(
        RefreshRequest? body, ISender sender, HttpContext httpContext, AuthCookies cookies, CancellationToken cancellationToken)
    {
        // Prefer the cookie (browser); fall back to the body (native/bearer client).
        var cookieToken = cookies.ReadRefresh(httpContext);
        var useCookies = !string.IsNullOrEmpty(cookieToken);
        var rawToken = useCookies ? cookieToken : body?.RefreshToken;

        if (string.IsNullOrEmpty(rawToken))
            throw new InvalidRefreshTokenException();

        // Cookie mode is CSRF-sensitive (the cookie is sent automatically), so require the double-submit token.
        if (useCookies && !cookies.ValidateCsrf(httpContext))
            throw new CsrfValidationException();

        var result = await sender.Send(new RefreshCommand(rawToken), cancellationToken);
        return cookies.WriteTokens(httpContext, result, useCookies);
    }
}
