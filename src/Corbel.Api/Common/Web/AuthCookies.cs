using Corbel.Common.Options;
using Microsoft.Extensions.Options;

namespace Corbel.Common.Web;

/// <summary>
/// Reads and writes the auth-transport cookies (refresh + CSRF) so the auth endpoints keep cookie mechanics out
/// of their handlers. The refresh cookie is httpOnly, path-scoped to the refresh endpoint, and capped at the
/// absolute refresh window; the access token is never a cookie (it rides the response body). Lives in the web
/// layer alongside <see cref="CsrfProtection"/>.
/// </summary>
public sealed class AuthCookies(
    IOptions<CookieAuthOptions> cookieOptions,
    IOptions<JwtOptions> jwtOptions,
    CsrfProtection csrf)
{
    private readonly CookieAuthOptions _cookie = cookieOptions.Value;
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public void WriteRefresh(HttpContext context, string rawRefreshToken)
        => context.Response.Cookies.Append(
            _cookie.RefreshCookieName, rawRefreshToken,
            Build(_cookie.RefreshCookiePath, httpOnly: true, TimeSpan.FromDays(_jwt.RefreshAbsoluteDays)));

    /// <summary>Issues the CSRF cookie bound to the session's refresh token (which is also written as the refresh cookie).</summary>
    public void IssueCsrf(HttpContext context, string rawRefreshToken) => csrf.Issue(context, rawRefreshToken);

    /// <summary>Validates the double-submit token against the refresh cookie presented on this request.</summary>
    public bool ValidateCsrf(HttpContext context)
    {
        var sessionToken = ReadRefresh(context);
        return sessionToken is not null && csrf.Validate(context, sessionToken);
    }

    public string? ReadRefresh(HttpContext context)
        => context.Request.Cookies.TryGetValue(_cookie.RefreshCookieName, out var value) ? value : null;

    /// <summary>Expires the refresh + CSRF cookies, matching the attributes each was written with so the browser drops them.</summary>
    public void Clear(HttpContext context)
    {
        context.Response.Cookies.Delete(_cookie.RefreshCookieName, Build(_cookie.RefreshCookiePath, httpOnly: true, maxAge: null));
        context.Response.Cookies.Delete(_cookie.CsrfCookieName, Build("/", httpOnly: false, maxAge: null));
    }

    private CookieOptions Build(string path, bool httpOnly, TimeSpan? maxAge) => new()
    {
        HttpOnly = httpOnly,
        Secure = _cookie.Secure,
        SameSite = _cookie.SameSite,
        Path = path,
        IsEssential = true,
        MaxAge = maxAge,
    };
}
