using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Corbel.Common.Options;

/// <summary>
/// Cookie-transport settings for the auth flow. In browser ("cookie") mode the refresh token rides an
/// httpOnly, path-scoped cookie and a non-httpOnly CSRF cookie backs the double-submit check on
/// <c>/api/auth/refresh</c>. (The access token is never a cookie — it is returned in the body and held in
/// memory by the SPA / sent as a bearer.) Bound from the "CookieAuth" section; defaults are production-sane.
/// </summary>
public sealed class CookieAuthOptions
{
    public const string SectionName = "CookieAuth";

    /// <summary>Name of the httpOnly refresh-token cookie.</summary>
    [Required]
    public string RefreshCookieName { get; set; } = "corbel_rt";

    /// <summary>
    /// Path the refresh cookie is scoped to. Must cover both endpoints that consume it — <c>/api/auth/refresh</c>
    /// (rotation) and <c>/api/auth/logout</c> (family revocation) — so the browser actually sends the cookie to
    /// logout; otherwise sign-out can't revoke the refresh family. <c>/api/auth</c> is the tightest prefix that
    /// spans both, still far narrower than "/".
    /// </summary>
    [Required]
    public string RefreshCookiePath { get; set; } = "/api/auth";

    /// <summary>Name of the non-httpOnly CSRF cookie the SPA echoes back in the <c>X-XSRF-TOKEN</c> header.</summary>
    [Required]
    public string CsrfCookieName { get; set; } = "XSRF-TOKEN";

    /// <summary>Emit cookies with the Secure attribute. Keep true everywhere except plain-HTTP local dev.</summary>
    public bool Secure { get; set; } = true;

    /// <summary>SameSite policy for the auth cookies. Lax is the safe default for a same-site SPA; use None (with Secure) for a cross-site SPA.</summary>
    public SameSiteMode SameSite { get; set; } = SameSiteMode.Lax;
}
