using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Corbel.Api.Tests.Fixtures;
using Shouldly;
using Xunit;

namespace Corbel.Api.Tests.Integration;

/// <summary>
/// Account-protection flows over the real host: failed-login lockout, anti-enumeration registration, and refresh
/// revocation on logout / password change. These exercise the security invariants that the happy-path auth tests
/// do not.
/// </summary>
[Collection(CorbelCollection.Name)]
public sealed class AuthSecurityApiTests(CorbelFixture fixture) : IAsyncLifetime
{
    // Mirrors IdentityOptions.Lockout.MaxFailedAccessAttempts configured in AuthServiceCollectionExtensions.
    private const int MaxFailedAttempts = 5;

    private readonly CorbelFixture _fixture = fixture;

    public async ValueTask InitializeAsync() => await _fixture.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Repeated_bad_passwords_lock_the_account_revealed_only_once_the_right_password_is_supplied()
    {
        const string email = "lockout@corbel.test";
        const string password = "Passw0rd!";
        await _fixture.Api.EnsureConfirmedUserAsync(email, password);

        using var client = _fixture.Api.CreateApiClient();

        for (var attempt = 1; attempt <= MaxFailedAttempts; attempt++)
        {
            var failed = await client.PostAsJsonAsync(
                "/api/auth/login", new { email, password = "wrong-password" }, cancellationToken: TestContext.Current.CancellationToken);

            failed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

            // A wrong password is always the generic code — even the attempt that trips the lockout — so the
            // response never reveals account existence to a caller who can't supply the password (anti-enumeration).
            (await failed.ReadErrorCodeAsync()).ShouldBe("auth.invalid_credentials", $"attempt {attempt}");
        }

        // The lock is real: supplying the CORRECT password now surfaces account_locked — disclosed only to the
        // owner who proved ownership, never to an enumerating attacker.
        var correct = await client.PostAsJsonAsync(
            "/api/auth/login", new { email, password }, cancellationToken: TestContext.Current.CancellationToken);

        correct.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await correct.ReadErrorCodeAsync()).ShouldBe("auth.account_locked");
    }

    [Fact]
    public async Task Re_registering_an_email_neither_leaks_existence_nor_overwrites_the_password()
    {
        const string email = "enumerate@corbel.test";
        const string firstPassword = "Passw0rd!";
        const string secondPassword = "Different1!";

        using var client = _fixture.Api.CreateApiClient();

        var first = await client.PostAsJsonAsync(
            "/api/auth/register", new { email, password = firstPassword, displayName = "First" }, cancellationToken: TestContext.Current.CancellationToken);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        // The same email with a different password returns the identical acknowledgement — no existence oracle.
        var second = await client.PostAsJsonAsync(
            "/api/auth/register", new { email, password = secondPassword, displayName = "Second" }, cancellationToken: TestContext.Current.CancellationToken);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);

        var firstBody = await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var secondBody = await second.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        secondBody.ShouldBe(firstBody);

        // The original password still works (no overwrite); the second never took effect (no account created).
        var loginFirst = await client.PostAsJsonAsync(
            "/api/auth/login", new { email, password = firstPassword }, cancellationToken: TestContext.Current.CancellationToken);
        loginFirst.StatusCode.ShouldBe(HttpStatusCode.OK);

        var loginSecond = await client.PostAsJsonAsync(
            "/api/auth/login", new { email, password = secondPassword }, cancellationToken: TestContext.Current.CancellationToken);
        loginSecond.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await loginSecond.ReadErrorCodeAsync()).ShouldBe("auth.invalid_credentials");
    }

    [Fact]
    public async Task Register_with_a_weak_password_is_a_uniform_validation_error_for_taken_and_free_emails()
    {
        // A long-but-simple password passes the length check but fails complexity. Because PasswordRules runs in
        // the validation pipeline before the handler (and Identity is length-only), it is rejected identically
        // whether the email is taken or free — so a weak password can't be used to probe account existence.
        const string takenEmail = "weak-taken@corbel.test";
        const string freeEmail = "weak-free@corbel.test";
        const string weakPassword = "passwordpassword"; // 16 chars, no digit, no uppercase

        await _fixture.Api.EnsureConfirmedUserAsync(takenEmail, "Passw0rd!");

        using var client = _fixture.Api.CreateApiClient();

        var taken = await client.PostAsJsonAsync(
            "/api/auth/register", new { email = takenEmail, password = weakPassword }, cancellationToken: TestContext.Current.CancellationToken);
        var free = await client.PostAsJsonAsync(
            "/api/auth/register", new { email = freeEmail, password = weakPassword }, cancellationToken: TestContext.Current.CancellationToken);

        taken.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        free.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await taken.ReadErrorCodeAsync()).ShouldBe("common.validation");
        (await free.ReadErrorCodeAsync()).ShouldBe("common.validation");
    }

    [Fact]
    public async Task Locked_account_cannot_refresh_an_existing_token()
    {
        const string email = "refresh-lockout@corbel.test";
        const string password = "Passw0rd!";
        await _fixture.Api.EnsureConfirmedUserAsync(email, password);

        using var client = _fixture.Api.CreateApiClient();

        // Issue a valid refresh token before the account is locked.
        var login = await client.PostAsJsonAsync(
            "/api/auth/login", new { email, password, useCookies = false }, cancellationToken: TestContext.Current.CancellationToken);
        var tokens = await login.ReadJsonAsync<TokenResponse>();
        tokens.RefreshToken.ShouldNotBeNullOrWhiteSpace();

        // Lock the account with repeated bad passwords (this resets the just-issued token's owner into a locked state).
        for (var attempt = 0; attempt < MaxFailedAttempts; attempt++)
            await client.PostAsJsonAsync(
                "/api/auth/login", new { email, password = "wrong-password" }, cancellationToken: TestContext.Current.CancellationToken);

        // Refresh re-checks lockout and fails closed even though the token itself is still otherwise valid.
        var refresh = await client.PostAsJsonAsync(
            "/api/auth/refresh", new { refreshToken = tokens.RefreshToken }, cancellationToken: TestContext.Current.CancellationToken);
        refresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await refresh.ReadErrorCodeAsync()).ShouldBe("auth.account_locked");
    }

    [Fact]
    public async Task Bearer_logout_revokes_the_refresh_token()
    {
        const string email = "logout-bearer@corbel.test";
        const string password = "Passw0rd!";
        await _fixture.Api.EnsureConfirmedUserAsync(email, password);

        using var client = _fixture.Api.CreateApiClient();

        var login = await client.PostAsJsonAsync(
            "/api/auth/login", new { email, password, useCookies = false }, cancellationToken: TestContext.Current.CancellationToken);
        var tokens = await login.ReadJsonAsync<TokenResponse>();
        tokens.RefreshToken.ShouldNotBeNullOrWhiteSpace();

        var logout = await client.PostAsJsonAsync(
            "/api/auth/logout", new { refreshToken = tokens.RefreshToken }, cancellationToken: TestContext.Current.CancellationToken);
        logout.StatusCode.ShouldBe(HttpStatusCode.OK);

        // The token's family was revoked, so it can no longer be rotated.
        var refresh = await client.PostAsJsonAsync(
            "/api/auth/refresh", new { refreshToken = tokens.RefreshToken }, cancellationToken: TestContext.Current.CancellationToken);
        refresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Changing_the_password_requires_the_current_one_and_revokes_existing_refresh_tokens()
    {
        const string email = "changepass@corbel.test";
        const string oldPassword = "Passw0rd!";
        const string newPassword = "N3wPassw0rd!";
        await _fixture.Api.EnsureConfirmedUserAsync(email, oldPassword);

        using var client = _fixture.Api.CreateApiClient();

        var login = await client.PostAsJsonAsync(
            "/api/auth/login", new { email, password = oldPassword, useCookies = false }, cancellationToken: TestContext.Current.CancellationToken);
        var tokens = await login.ReadJsonAsync<TokenResponse>();
        tokens.RefreshToken.ShouldNotBeNullOrWhiteSpace();

        using var authed = _fixture.Api.CreateApiClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        // Wrong current password → rejected as a validation problem, nothing revoked.
        var wrong = await authed.PostAsJsonAsync(
            "/api/auth/change-password", new { currentPassword = "not-my-password", newPassword }, cancellationToken: TestContext.Current.CancellationToken);
        wrong.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await wrong.ReadErrorCodeAsync()).ShouldBe("common.validation");

        // Correct current password → success.
        var changed = await authed.PostAsJsonAsync(
            "/api/auth/change-password", new { currentPassword = oldPassword, newPassword }, cancellationToken: TestContext.Current.CancellationToken);
        changed.StatusCode.ShouldBe(HttpStatusCode.OK);

        // The refresh token issued before the change is now revoked.
        var refresh = await client.PostAsJsonAsync(
            "/api/auth/refresh", new { refreshToken = tokens.RefreshToken }, cancellationToken: TestContext.Current.CancellationToken);
        refresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Auth_endpoints_rate_limit_a_burst_and_return_a_retry_after_hint()
    {
        // A dedicated host that keeps the REAL "auth" rate-limit policy — the shared fixture disables rate limiting
        // for determinism, so this is the one place the 429 control is actually exercised. Only the global per-user
        // limiter is neutralized so it can't interfere with the burst.
        await using var rateLimited = new ApiFactory(_fixture.Postgres.ConnectionString, enableAuthRateLimit: true);
        using var client = rateLimited.CreateApiClient();

        // The "auth" policy is a fixed window of 10/min (partitioned by client IP, constant under the test host),
        // so a burst past the limit must eventually be rejected with 429 + a Retry-After backoff hint. Bad
        // credentials are fine — the limiter counts requests before the handler runs.
        HttpResponseMessage? limited = null;
        for (var attempt = 0; attempt < 15 && limited is null; attempt++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/auth/login", new { email = "burst@corbel.test", password = "whatever" },
                cancellationToken: TestContext.Current.CancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                limited = response;
        }

        limited.ShouldNotBeNull("a burst of auth requests should eventually be rate limited");
        limited.Headers.RetryAfter.ShouldNotBeNull();
        (await limited.ReadErrorCodeAsync()).ShouldBe("common.rate_limited");
    }
}
