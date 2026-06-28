using System.Net;
using System.Net.Http.Json;
using Corbel.Api.Tests.Fixtures;
using Corbel.Common.Options;
using Corbel.Common.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Corbel.Api.Tests.Integration;

/// <summary>
/// Cookie ("browser") transport for the auth flow: login drops an httpOnly refresh cookie plus a readable CSRF
/// cookie, and the cookie-sensitive endpoints (refresh, logout) enforce the double-submit CSRF check before
/// touching the refresh family. Cookies are tracked by a hand-rolled <see cref="CookieJar"/> so the test can
/// decide whether to echo the XSRF-TOKEN back, exercising both the honoured and the rejected paths.
/// </summary>
[Collection(CorbelCollection.Name)]
public sealed class CookieAuthApiTests(CorbelFixture fixture) : IAsyncLifetime
{
    private readonly CorbelFixture _fixture = fixture;

    public async ValueTask InitializeAsync() => await _fixture.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Cookie_login_sets_httponly_refresh_and_readable_csrf_cookies()
    {
        const string email = "cookie-login@corbel.test";
        const string password = "Passw0rd!";
        await _fixture.Api.EnsureConfirmedUserAsync(email, password);
        var cookieOptions = ResolveCookieOptions();

        using var session = _fixture.Api.CreateCookieClient();
        var login = await session.Client.PostAsJsonAsync(
            "/api/auth/login", new { email, password, useCookies = true }, cancellationToken: TestContext.Current.CancellationToken);

        login.StatusCode.ShouldBe(HttpStatusCode.OK);

        var refreshCookie = FindSetCookie(login, cookieOptions.RefreshCookieName);
        var csrfCookie = FindSetCookie(login, cookieOptions.CsrfCookieName);

        // The refresh cookie is httpOnly (never readable by JS); the CSRF cookie must be readable to be echoed back.
        HasFlag(refreshCookie, "httponly").ShouldBeTrue();
        HasFlag(csrfCookie, "httponly").ShouldBeFalse();

        // Secure tracks the configured posture rather than a hardcoded expectation.
        HasFlag(refreshCookie, "secure").ShouldBe(cookieOptions.Secure);
        HasFlag(csrfCookie, "secure").ShouldBe(cookieOptions.Secure);
    }

    [Fact]
    public async Task Cookie_refresh_with_valid_csrf_header_rotates_the_refresh_cookie()
    {
        const string email = "cookie-refresh@corbel.test";
        const string password = "Passw0rd!";
        await _fixture.Api.EnsureConfirmedUserAsync(email, password);
        var cookieOptions = ResolveCookieOptions();

        using var session = _fixture.Api.CreateCookieClient();
        await Login(session, email, password);

        var originalRefresh = session.Jar[cookieOptions.RefreshCookieName];
        originalRefresh.ShouldNotBeNullOrWhiteSpace();
        var csrf = session.Jar[cookieOptions.CsrfCookieName]!;

        var refresh = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh") { Content = JsonContent.Create(new { }) };
        refresh.Headers.Add(CsrfProtection.HeaderName, csrf);
        var response = await session.Client.SendAsync(refresh, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        // A successful rotation issues a fresh refresh cookie (the jar now holds the rotated value).
        session.Jar[cookieOptions.RefreshCookieName].ShouldNotBe(originalRefresh);
    }

    [Fact]
    public async Task Cookie_refresh_without_or_with_forged_csrf_header_is_forbidden()
    {
        const string email = "cookie-csrf@corbel.test";
        const string password = "Passw0rd!";
        await _fixture.Api.EnsureConfirmedUserAsync(email, password);

        using var session = _fixture.Api.CreateCookieClient();
        await Login(session, email, password);

        // No double-submit header: the auto-sent refresh cookie alone must not be enough to rotate.
        var missing = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh") { Content = JsonContent.Create(new { }) };
        var missingResponse = await session.Client.SendAsync(missing, TestContext.Current.CancellationToken);
        missingResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await missingResponse.ReadErrorCodeAsync()).ShouldBe("common.forbidden");

        // A header that does not match the cookie (a sibling subdomain cannot forge it) is rejected too.
        var forged = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh") { Content = JsonContent.Create(new { }) };
        forged.Headers.Add(CsrfProtection.HeaderName, "forged-csrf-token-value");
        var forgedResponse = await session.Client.SendAsync(forged, TestContext.Current.CancellationToken);
        forgedResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await forgedResponse.ReadErrorCodeAsync()).ShouldBe("common.forbidden");
    }

    [Fact]
    public async Task Cookie_logout_with_valid_csrf_revokes_the_refresh_family()
    {
        const string email = "cookie-logout@corbel.test";
        const string password = "Passw0rd!";
        await _fixture.Api.EnsureConfirmedUserAsync(email, password);
        var cookieOptions = ResolveCookieOptions();

        using var session = _fixture.Api.CreateCookieClient();
        await Login(session, email, password);

        // Capture the cookies before logout clears them so the family can be replayed afterwards.
        var refreshValue = session.Jar[cookieOptions.RefreshCookieName]!;
        var csrf = session.Jar[cookieOptions.CsrfCookieName]!;

        var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout") { Content = JsonContent.Create(new { }) };
        logout.Headers.Add(CsrfProtection.HeaderName, csrf);
        var logoutResponse = await session.Client.SendAsync(logout, TestContext.Current.CancellationToken);
        logoutResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Replay the original cookies by hand (the jar dropped them on logout). CSRF still validates, so a 401
        // proves the *family* was revoked server-side rather than merely the cookies being cleared.
        var replay = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh") { Content = JsonContent.Create(new { }) };
        replay.Headers.TryAddWithoutValidation(
            "Cookie", $"{cookieOptions.RefreshCookieName}={refreshValue}; {cookieOptions.CsrfCookieName}={csrf}");
        replay.Headers.Add(CsrfProtection.HeaderName, csrf);
        var replayResponse = await session.Client.SendAsync(replay, TestContext.Current.CancellationToken);

        replayResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private CookieAuthOptions ResolveCookieOptions() =>
        _fixture.Api.Services.GetRequiredService<IOptions<CookieAuthOptions>>().Value;

    private static async Task Login(CookieSession session, string email, string password)
    {
        var login = await session.Client.PostAsJsonAsync(
            "/api/auth/login", new { email, password, useCookies = true }, cancellationToken: TestContext.Current.CancellationToken);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static string FindSetCookie(HttpResponseMessage response, string name) =>
        response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith($"{name}=", StringComparison.Ordinal));

    /// <summary>True when the Set-Cookie carries the given attribute flag (HttpOnly/Secure), matched as a whole segment.</summary>
    private static bool HasFlag(string setCookie, string flag) =>
        setCookie.Split(';').Any(part => part.Trim().Equals(flag, StringComparison.OrdinalIgnoreCase));
}
