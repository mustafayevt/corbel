using Corbel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Corbel.Api.Tests.Fixtures;

/// <summary>
/// Spins up a throwaway PostgreSQL 17 container for the whole test collection, builds the schema by applying
/// the committed EF migrations once (the exact path production uses — not EnsureCreated), and truncates every
/// table between tests for a clean slate. No external reset library: a Postgres-only template carries no SQL
/// Server driver.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("corbel")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    /// <summary>Npgsql connection string for the running container; injected into the host as <c>ConnectionStrings:corbel</c>.</summary>
    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        // Apply the committed migrations through a standalone context (same convention as the host) so the
        // host's startup migrate is a no-op and CI exercises the real, shipped migration.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();
    }

    /// <summary>Truncates every table (except the EF migrations history) back to empty between tests.</summary>
    public async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        const string truncateAll = """
            DO $$
            DECLARE tbl RECORD;
            BEGIN
                FOR tbl IN
                    SELECT tablename FROM pg_tables
                    WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory'
                LOOP
                    EXECUTE 'TRUNCATE TABLE public.' || quote_ident(tbl.tablename) || ' RESTART IDENTITY CASCADE';
                END LOOP;
            END $$;
            """;

        await using var command = new NpgsqlCommand(truncateAll, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}
