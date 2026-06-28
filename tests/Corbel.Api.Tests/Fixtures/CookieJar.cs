namespace Corbel.Api.Tests.Fixtures;

/// <summary>
/// Minimal client-side cookie jar for the cookie-mode auth tests. It captures Set-Cookie values from each
/// response and replays them on the next request by hand — deliberately not a real <c>CookieContainer</c>, which
/// withholds Secure cookies over the plain-http test transport (so the production-default <c>CookieAuth.Secure</c>
/// posture stays testable). The readable XSRF-TOKEN it captures can be echoed into the <c>X-XSRF-TOKEN</c> header
/// to drive the double-submit CSRF check. Cookie <c>Path</c> scoping is intentionally ignored: these tests only
/// call <c>/api/auth/*</c>, which the refresh cookie is scoped to anyway.
/// </summary>
public sealed class CookieJar : DelegatingHandler
{
    private readonly Dictionary<string, string> _cookies = new(StringComparer.Ordinal);

    /// <summary>The current value of a stored cookie, or null if it was never set or has since been cleared.</summary>
    public string? this[string name] => _cookies.GetValueOrDefault(name);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Replay every stored cookie unless the caller set the header explicitly (e.g. a hand-crafted replay).
        if (_cookies.Count > 0 && !request.Headers.Contains("Cookie"))
            request.Headers.TryAddWithoutValidation(
                "Cookie", string.Join("; ", _cookies.Select(c => $"{c.Key}={c.Value}")));

        var response = await base.SendAsync(request, cancellationToken);

        if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
            foreach (var setCookie in setCookies)
                Capture(setCookie);

        return response;
    }

    private void Capture(string setCookieHeader)
    {
        // Only the leading "name=value" matters; the attributes (Path, Secure, HttpOnly, ...) follow the first ';'.
        var pair = setCookieHeader.Split(';', 2)[0];
        var separator = pair.IndexOf('=', StringComparison.Ordinal);
        if (separator <= 0)
            return;

        var name = pair[..separator].Trim();
        var value = pair[(separator + 1)..].Trim();

        if (string.IsNullOrEmpty(value))
            _cookies.Remove(name); // an expired/cleared cookie (logout, change-password)
        else
            _cookies[name] = value;
    }
}

/// <summary>A cookie-mode <see cref="HttpClient"/> paired with the <see cref="CookieJar"/> tracking its cookies; dispose to release the client.</summary>
public sealed class CookieSession(HttpClient client, CookieJar jar) : IDisposable
{
    public HttpClient Client { get; } = client;
    public CookieJar Jar { get; } = jar;

    public void Dispose() => Client.Dispose();
}
