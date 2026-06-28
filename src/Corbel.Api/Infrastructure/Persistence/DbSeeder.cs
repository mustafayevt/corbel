using Corbel.Common;
using Corbel.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Corbel.Infrastructure.Persistence;

/// <summary>
/// Idempotent startup seeding: ensures the <see cref="AppRoles"/> exist and, when <c>Seed:AdminEmail</c> +
/// <c>Seed:AdminPassword</c> are configured, provisions a single confirmed admin user. Safe to run on every
/// boot AND concurrently across replicas (a transaction-scoped Postgres advisory lock serializes the seed so two
/// instances racing a fresh database can't both pass the existence checks and then collide on a unique index);
/// fails loud if Identity rejects a role/user so a misconfiguration can't leave a half-seeded state.
/// </summary>
public static class DbSeeder
{
    // Arbitrary, app-specific key for pg_advisory_xact_lock so only one replica seeds at a time. The lock is
    // released automatically when the wrapping transaction commits, and never blocks ordinary application queries.
    private const long SeedAdvisoryLockKey = 0x436F_7262_656C_5345; // "Corbel" + "SE"(ed)

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var db = services.GetRequiredService<AppDbContext>();

        // RoleManager/UserManager resolve their store from the SAME scoped AppDbContext, so the advisory-locked
        // transaction opened here is ambient for every write below — they commit together when it commits.
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({SeedAdvisoryLockKey})", cancellationToken);

            await SeedRolesAndAdminAsync(services, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        });
    }

    private static async Task SeedRolesAndAdminAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in AppRoles.All)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await roleManager.RoleExistsAsync(role))
                continue;

            var roleResult = await roleManager.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.CreateVersion7() });
            if (!roleResult.Succeeded)
                throw new InvalidOperationException($"Failed to seed role '{role}': {Describe(roleResult)}");
        }

        var configuration = services.GetRequiredService<IConfiguration>();
        var adminEmail = configuration["Seed:AdminEmail"];
        var adminPassword = configuration["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
            return;

        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        if (await userManager.FindByEmailAsync(adminEmail) is not null)
            return;

        var admin = new AppUser
        {
            Id = Guid.CreateVersion7(),
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true,
            DisplayName = "Administrator",
        };

        var created = await userManager.CreateAsync(admin, adminPassword);
        if (!created.Succeeded)
            throw new InvalidOperationException($"Failed to seed admin user '{adminEmail}': {Describe(created)}");

        var roleAssigned = await userManager.AddToRoleAsync(admin, AppRoles.Admin);
        if (!roleAssigned.Succeeded)
            throw new InvalidOperationException($"Failed to grant Admin to '{adminEmail}': {Describe(roleAssigned)}");
    }

    private static string Describe(IdentityResult result) => string.Join("; ", result.Errors.Select(e => e.Description));
}
