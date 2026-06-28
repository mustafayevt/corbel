using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Corbel.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so <c>dotnet ef migrations</c> can build the context without booting the host. Reads
/// the connection string from <c>ConnectionStrings__corbel</c> (set by Aspire/CI), falling back to a
/// local-dev default.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string DevConnectionString =
        "Host=localhost;Database=corbel;Username=postgres;Password=postgres";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__corbel") ?? DevConnectionString;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention() // must match the runtime convention so migrations scaffold snake_case
            .Options;

        return new AppDbContext(options);
    }
}
