using System.Net;
using System.Net.Http.Json;
using Corbel.Api.Tests.Fixtures;
using Shouldly;
using Xunit;

namespace Corbel.Api.Tests.Integration;

/// <summary>
/// The 400 ValidationProblem contract the TS client depends on: RFC 9457 <c>application/problem+json</c>, the
/// stable <c>common.validation</c> error code, and a non-empty <c>errors</c> map keyed by FluentValidation's
/// PascalCase property names (not camelCase). Asserted on one authenticated surface (notes) and one anonymous
/// surface (register).
/// </summary>
[Collection(CorbelCollection.Name)]
public sealed class ValidationContractApiTests(CorbelFixture fixture) : IAsyncLifetime
{
    private readonly CorbelFixture _fixture = fixture;

    public async ValueTask InitializeAsync() => await _fixture.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Creating_a_note_with_a_blank_title_returns_the_validation_problem_contract()
    {
        using var client = await _fixture.Api.CreateAuthenticatedClientAsync("validation-notes@corbel.test", "Passw0rd!");

        var response = await client.PostAsJsonAsync(
            "/api/notes", new { title = "", content = "body" }, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var problem = await response.ReadProblemAsync();
        problem.GetProperty("errorCode").GetString().ShouldBe("common.validation");

        var errors = problem.GetProperty("errors");
        errors.EnumerateObject().Any().ShouldBeTrue();
        errors.TryGetProperty("Title", out _).ShouldBeTrue();   // FluentValidation PropertyName is PascalCase
        errors.TryGetProperty("title", out _).ShouldBeFalse();  // not camelCased on the way out
    }

    [Fact]
    public async Task Registering_with_a_bad_email_and_short_password_returns_the_validation_problem_contract()
    {
        using var client = _fixture.Api.CreateApiClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register", new { email = "not-an-email", password = "short" }, cancellationToken: TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var problem = await response.ReadProblemAsync();
        problem.GetProperty("errorCode").GetString().ShouldBe("common.validation");

        var errors = problem.GetProperty("errors");
        errors.EnumerateObject().Any().ShouldBeTrue();
        errors.TryGetProperty("Email", out _).ShouldBeTrue();
        errors.TryGetProperty("Password", out _).ShouldBeTrue();
        errors.TryGetProperty("email", out _).ShouldBeFalse();
        errors.TryGetProperty("password", out _).ShouldBeFalse();
    }
}
