using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Corbel.Common.Options;
using Microsoft.Extensions.Options;

namespace Corbel.Common.Web;

/// <summary>
/// Signed, session-bound double-submit CSRF protection for the cookie-mode auth endpoints (refresh, logout).
/// <see cref="Issue"/> mints a random nonce, HMACs it together with the session's refresh token, and writes
/// <c>nonce.signature</c> into a readable cookie the SPA echoes back in the <see cref="HeaderName"/> header.
/// <see cref="Validate"/> requires header == cookie (constant-time) AND a signature that matches the refresh
/// token presented on THIS request — so a token minted for a different session (e.g. one an attacker obtained
/// by logging in themselves and tried to plant via a sibling-subdomain cookie) is rejected, even under the
/// documented cross-site SameSite=None configuration where the cookie alone is not enough.
/// </summary>
public sealed class CsrfProtection(IOptions<CookieAuthOptions> cookieOptions, IOptions<JwtOptions> jwtOptions)
{
    public const string HeaderName = "X-XSRF-TOKEN";

    private readonly CookieAuthOptions _cookie = cookieOptions.Value;

    // Match the persistent refresh cookie's lifetime so the CSRF cookie survives a browser restart; otherwise the
    // cold-start silent refresh (which needs both cookies for the double-submit check) would 403 despite a
    // still-valid "remember me" session.
    private readonly TimeSpan _cookieMaxAge = TimeSpan.FromDays(jwtOptions.Value.RefreshAbsoluteDays);

    // Dedicated CSRF key derived from the JWT signing key via HKDF (labeled domain separation), so the CSRF HMAC
    // and the JWT signature never share key material without managing a second secret.
    private readonly byte[] _key = HKDF.DeriveKey(
        HashAlgorithmName.SHA256,
        Encoding.UTF8.GetBytes(jwtOptions.Value.SigningKey),
        outputLength: 32,
        info: "corbel-csrf-v1"u8.ToArray());

    /// <summary>Issues a CSRF token bound to <paramref name="sessionToken"/> (the session's refresh token) and writes the readable cookie.</summary>
    public string Issue(HttpContext context, string sessionToken)
    {
        var nonce = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var token = $"{nonce}.{Sign(nonce, sessionToken)}";

        context.Response.Cookies.Append(_cookie.CsrfCookieName, token, new CookieOptions
        {
            HttpOnly = false, // must be readable by JS to echo into the request header
            Secure = _cookie.Secure,
            SameSite = _cookie.SameSite,
            Path = "/",
            IsEssential = true,
            MaxAge = _cookieMaxAge,
        });

        return token;
    }

    /// <summary>True only when the request carries a CSRF cookie and a matching header validly signed for <paramref name="sessionToken"/>.</summary>
    public bool Validate(HttpContext context, string sessionToken)
    {
        if (!context.Request.Cookies.TryGetValue(_cookie.CsrfCookieName, out var cookieToken) ||
            string.IsNullOrEmpty(cookieToken))
            return false;

        var headerToken = context.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrEmpty(headerToken))
            return false;

        return FixedTimeEquals(headerToken, cookieToken) && VerifySignature(cookieToken, sessionToken);
    }

    private string Sign(string nonce, string sessionToken)
        => Base64Url.EncodeToString(HMACSHA256.HashData(_key, Encoding.ASCII.GetBytes($"{nonce}.{sessionToken}")));

    private bool VerifySignature(string token, string sessionToken)
    {
        var separator = token.IndexOf('.', StringComparison.Ordinal);
        if (separator <= 0 || separator == token.Length - 1)
            return false;

        var nonce = token[..separator];
        var providedSignature = token[(separator + 1)..];
        return FixedTimeEquals(providedSignature, Sign(nonce, sessionToken));
    }

    private static bool FixedTimeEquals(string a, string b)
        => CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(a), Encoding.ASCII.GetBytes(b));
}
