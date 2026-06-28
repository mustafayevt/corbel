using Corbel.Common.Exceptions;
using Corbel.Common.Web;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;

namespace Corbel.Features.Auth;

/// <summary>Maps an ASP.NET Core Identity failure into the app's <see cref="ValidationException"/> (rendered as a 400 with field errors).</summary>
internal static class IdentityResultExtensions
{
    public static ValidationException ToValidationException(this IdentityResult result)
    {
        var errors = result.Errors
            .GroupBy(FieldFor, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray(), StringComparer.Ordinal);

        return new ValidationException(errors);
    }

    private static string FieldFor(IdentityError error)
    {
        if (error.Code.Contains("Password", StringComparison.OrdinalIgnoreCase))
            return "password";
        if (error.Code.Contains("Email", StringComparison.OrdinalIgnoreCase) ||
            error.Code.Contains("UserName", StringComparison.OrdinalIgnoreCase))
            return "email";
        return string.Empty;
    }
}

/// <summary>Shared cookie-vs-bearer transport handling for the token-issuing auth endpoints (login, refresh).</summary>
internal static class TokenTransport
{
    /// <summary>
    /// In bearer mode returns the token envelope as-is. In cookie mode writes the httpOnly refresh cookie + CSRF
    /// token and strips the refresh token from the body — the security invariant that it must never be readable
    /// by JS in cookie mode lives in this one place.
    /// </summary>
    public static Ok<TokenResponse> WriteTokens(this AuthCookies cookies, HttpContext context, TokenResponse result, bool useCookies)
    {
        if (!useCookies)
            return TypedResults.Ok(result);

        cookies.WriteRefresh(context, result.RefreshToken!);
        cookies.IssueCsrf(context, result.RefreshToken!);
        return TypedResults.Ok(result with { RefreshToken = null });
    }
}
