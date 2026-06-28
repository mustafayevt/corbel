using Corbel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Corbel.Api.Tests.Fixtures;

/// <summary>
/// Collection-shared root: owns the Postgres container and the API host, wiring the container's connection
/// string into the host. <see cref="ResetAsync"/> truncates the database and re-seeds the Identity roles the
/// app expects, giving each test a clean, consistent starting state.
/// </summary>
public sealed class CorbelFixture : IAsyncLifetime
{
    public PostgresFixture Postgres { get; } = new();
    public ApiFactory Api { get; private set; } = default!;

    public async ValueTask InitializeAsync()
    {
        await Postgres.InitializeAsync();
        Api = new ApiFactory(Postgres.ConnectionString);

        // Force the host to build/start now (runs its one-time startup seeding) before the first test runs.
        _ = Api.Services;
    }

    /// <summary>Clean slate for the next test: empty tables, then re-ensure the Identity roles.</summary>
    public async Task ResetAsync()
    {
        await Postgres.ResetAsync();
        using var scope = Api.Services.CreateScope();
        await DbSeeder.SeedAsync(scope.ServiceProvider);
    }

    public async ValueTask DisposeAsync()
    {
        await Api.DisposeAsync();
        await Postgres.DisposeAsync();
    }
}

/// <summary>Shares one container + host across every integration test, which therefore run serialized.</summary>
[CollectionDefinition(Name)]
public sealed class CorbelCollection : ICollectionFixture<CorbelFixture>
{
    public const string Name = "Corbel";
}
