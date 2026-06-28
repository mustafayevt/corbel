using Corbel.Common;
using Corbel.Common.Exceptions;
using Corbel.Common.Web;
using Corbel.Domain.Entities;
using Corbel.Infrastructure.Auth;
using FluentValidation;
using Mediator;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace Corbel.Features.Auth;

/// <summary>Credentials plus the transport selector for a sign-in request.</summary>
/// <param name="Email">The account's email address.</param>
/// <param name="Password">The account's password.</param>
/// <param name="UseCookies">Selects the transport: <c>true</c> (the browser default) sets the httpOnly refresh cookie + CSRF token and omits the refresh token from the body; <c>false</c> returns the refresh token in the body (native/bearer clients).</param>
// Plain IRequest (not a transaction-wrapped command): the failed-attempt lockout counter must persist even
// though an invalid login then throws 401, so this slice manages its own writes.
public sealed record LoginCommand(string Email, string Password, bool UseCookies = true) : IRequest<TokenResponse>;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        // Bound the length before it reaches Argon2 (no valid stored password exceeds the policy max).
        RuleFor(x => x.Password).NotEmpty().MaximumLength(PasswordPolicy.MaxLength);
    }
}

public sealed class LoginHandler(
    UserManager<AppUser> userManager,
    IPasswordHasher<AppUser> passwordHasher,
    JwtTokenService jwtTokens,
    RefreshTokenService refreshTokens) : IRequestHandler<LoginCommand, TokenResponse>
{
    public async ValueTask<TokenResponse> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(command.Email.Trim());
        if (user is null)
        {
            // Equalize timing against the password-verify path so a missing account is indistinguishable.
            _ = passwordHasher.HashPassword(new AppUser(), command.Password);
            throw new InvalidCredentialsException();
        }

        // Verify the password BEFORE consulting lockout, so the response can never reveal account existence to a
        // caller who can't prove ownership. A wrong password always yields the generic InvalidCredentials — even
        // once the failure trips the lockout — so an attacker sees the same reply for an unknown email, a wrong
        // password, and a locked account. Lockout is disclosed only to someone who supplied the correct password
        // (the legitimate owner), which preserves the lockout UX without leaking enumeration. Every branch runs
        // one Argon2 verify, equalizing the dominant cost; the wrong-password branch also writes AccessFailedAsync,
        // a small residual asymmetry that is negligible beside the hash and network jitter.
        if (!await userManager.CheckPasswordAsync(user, command.Password))
        {
            await userManager.AccessFailedAsync(user);
            throw new InvalidCredentialsException();
        }

        if (await userManager.IsLockedOutAsync(user))
            throw new AccountLockedException();

        await userManager.ResetAccessFailedCountAsync(user);

        var roles = await userManager.GetRolesAsync(user);
        var (accessToken, expiresIn) = jwtTokens.CreateAccessToken(user, roles);
        var refreshToken = await refreshTokens.IssueAsync(user.Id, cancellationToken);

        return new TokenResponse(accessToken, expiresIn, refreshToken);
    }
}

public sealed class LoginEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("auth/login", Handle)
            .WithName("Login")
            .WithTags("Auth")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .WithSummary("Sign in and obtain an access token.")
            .WithDescription(
                "Verifies credentials and issues a short-lived JWT access token plus a refresh token (delivered per the `useCookies` transport selector). An unknown email, a wrong password, and a locked account all return the same 401 (anti-enumeration).\n\n"
                + "**Errors:** 401 `auth.invalid_credentials` or `auth.account_locked`, 400 `common.validation`, 429 `common.rate_limited`.")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesValidationProblem();

    private static async Task<Ok<TokenResponse>> Handle(
        LoginCommand command, ISender sender, HttpContext httpContext, AuthCookies cookies, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return cookies.WriteTokens(httpContext, result, command.UseCookies);
    }
}
