using Corbel.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Corbel.Infrastructure.Persistence;

/// <summary>
/// Wires up the persistence layer: <see cref="TimeProvider"/>, the scoped auditing/soft-delete SaveChanges
/// interceptor, and the PostgreSQL-backed <see cref="AppDbContext"/> with snake_case naming and transient-fault
/// retries enabled. (Domain events are dispatched post-commit by <c>TransactionBehavior</c>, not an interceptor.)
/// </summary>
public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<AuditingInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
            options
                .UseNpgsql(
                    configuration.GetConnectionString("corbel"),
                    npg => npg.EnableRetryOnFailure())
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(sp.GetRequiredService<AuditingInterceptor>()));

        return services;
    }
}
