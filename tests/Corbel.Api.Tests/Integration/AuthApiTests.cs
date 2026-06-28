using System.Net;
using System.Net.Http.Json;
using Corbel.Api.Tests.Fixtures;
using Shouldly;
using Xunit;

namespace Corbel.Api.Tests.Integration;

/// <summary>
/// Auth flows over the real host: the register → login happy path, credential errors, and refresh-token
/// rotation + reuse detection. The lean core has no email/forgot-password flow — see the README for adding
/// an IEmailSender + reset slice.
/// </summary>
[Collection(CorbelCollection.Name)]
public sealed class AuthApiTests(CorbelFixture fixture) : IAsyncLifetime
{
    private readonly CorbelFixture _fixture = fixture;

    public async ValueTask InitializeAsync() => await _fixture.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Register_then_login_returns_access_token()
    {
        using var client = _fixture.Api.CreateApiClient();
        const string email = "newuser@corbel.test";
        const string password = "Passw0rd!";

        var register = await client.PostAsJsonAsync("/api/auth/register", new { email, password, displayName = "New User" }, cancellationToken: TestContext.Current.CancellationToken);
        register.IsSuccessStatusCode.ShouldBeTrue($"register failed with {register.StatusCode}");

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password }, cancellationToken: TestContext.Current.CancellationToken);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);

        var token = await login.ReadJsonAsync<TokenResponse>();
        token.AccessToken.ShouldNotBeNullOrWhiteSpace();
        token.ExpiresIn.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401_problem_details()
    {
        const string email = "wrongpass@corbel.test";
        await _fixture.Api.EnsureConfirmedUserAsync(email, "Passw0rd!");

        using var client = _fixture.Api.CreateApiClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "totally-wrong" }, cancellationToken: TestContext.Current.CancellationToken);

        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await login.ReadErrorCodeAsync()).ShouldBe("auth.invalid_credentials");
    }

    [Fact]
    public async Task Refresh_rotates_tokens_and_revokes_the_family_on_reuse()
    {
        const string email = "rotate@corbel.test";
        const string password = "Passw0rd!";
        await _fixture.Api.EnsureConfirmedUserAsync(email, password);

        using var client = _fixture.Api.CreateApiClient();

        // Bearer mode (useCookies:false) returns the refresh token in the body.
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password, useCookies = false }, cancellationToken: TestContext.Current.CancellationToken);
        var first = await login.ReadJsonAsync<TokenResponse>();
        first.RefreshToken.ShouldNotBeNullOrWhiteSpace();

        // Rotate twice: t1 -> t2 -> t3. Each rotation consumes its parent and issues a new token.
        var t2 = await Rotate(client, first.RefreshToken!);
        t2.ShouldNotBe(first.RefreshToken);
        var t3 = await Rotate(client, t2);
        t3.ShouldNotBe(t2);

        // Replaying the original (consumed) token whose child is also consumed → reuse detected → family revoked.
        var reuse = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first.RefreshToken }, cancellationToken: TestContext.Current.CancellationToken);
        reuse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await reuse.ReadErrorCodeAsync()).ShouldBe("auth.token_reuse_detected");

        // The latest token is now part of the revoked family and no longer works either.
        var afterRevoke = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = t3 }, cancellationToken: TestContext.Current.CancellationToken);
        afterRevoke.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static async Task<string> Rotate(HttpClient client, string refreshToken)
    {
        var response = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken }, cancellationToken: TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var rotated = await response.ReadJsonAsync<TokenResponse>();
        rotated.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        return rotated.RefreshToken!;
    }
}
