using Corbel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Corbel.Setup;

/// <summary>
/// Applies EF migrations (when <c>Database:MigrateOnStartup</c> is true — the default) and seeds roles +
/// the dev admin once at host startup. As an <see cref="IHostedService"/> it runs only when the app actually
/// starts, so design-time tooling that builds the host without starting it needs no database. Serious
/// deployments set <c>Database:MigrateOnStartup=false</c> and migrate out-of-process (bundle / just migrate).
/// </summary>
public sealed class DatabaseInitializer(IServiceProvider serviceProvider, IConfiguration configuration)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var services = scope.ServiceProvider;

        if (configuration.GetValue("Database:MigrateOnStartup", defaultValue: true))
            await services.GetRequiredService<AppDbContext>().Database.MigrateAsync(cancellationToken);

        await DbSeeder.SeedAsync(services, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
