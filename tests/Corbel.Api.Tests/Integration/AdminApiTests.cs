using System.Net;
using Corbel.Api.Tests.Fixtures;
using Corbel.Features.Auth;
using Shouldly;
using Xunit;

namespace Corbel.Api.Tests.Integration;

/// <summary>Authorization surface: the role-gated Admin policy and the /me identity endpoint.</summary>
[Collection(CorbelCollection.Name)]
public sealed class AdminApiTests(CorbelFixture fixture) : IAsyncLifetime
{
    private readonly CorbelFixture _fixture = fixture;

    public async ValueTask InitializeAsync() => await _fixture.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Admin_endpoint_is_forbidden_for_a_regular_user()
    {
        using var client = await _fixture.Api.CreateAuthenticatedClientAsync("regular@corbel.test", "Passw0rd!");

        var response = await client.GetAsync("/api/admin/ping", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_endpoint_is_allowed_for_an_admin_user()
    {
        using var client = await _fixture.Api.CreateAdminClientAsync("boss@corbel.test", "Passw0rd!");

        var response = await client.GetAsync("/api/admin/ping", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Me_returns_the_authenticated_identity_and_roles()
    {
        using var client = await _fixture.Api.CreateAdminClientAsync("me@corbel.test", "Passw0rd!");

        var me = await (await client.GetAsync("/api/auth/me", TestContext.Current.CancellationToken))
            .ReadJsonAsync<UserResponse>();

        me.Email.ShouldBe("me@corbel.test");
        me.Roles.ShouldContain("Admin");
    }
}
