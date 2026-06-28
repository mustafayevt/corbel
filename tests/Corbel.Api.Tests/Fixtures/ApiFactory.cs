using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.RateLimiting;
using Corbel.Common;
using Corbel.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Corbel.Api.Tests.Fixtures;

/// <summary>
/// Boots the real <c>Corbel.Api</c> host against the Testcontainers database. Overrides only what a test
/// host needs: the container connection string (<c>ConnectionStrings:corbel</c>), a valid <c>Jwt:*</c>
/// signing config, the "Testing" environment (so the host skips dev-only migrate/OpenAPI), and disabled rate
/// limiting for determinism. Provides helpers to provision confirmed users and authenticated clients.
/// </summary>
public sealed class ApiFactory(string connectionString, bool enableAuthRateLimit = false) : WebApplicationFactory<Program>
{
    // 32+ chars: JwtOptions enforces a 256-bit key or the host refuses to boot.
    private const string TestSigningKey = "corbel-integration-tests-signing-key-please-change-0123456789";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
(StringComparer.Ordinal)
        {
            ["ConnectionStrings:corbel"] = connectionString,
            ["Jwt:SigningKey"] = TestSigningKey,
            ["Jwt:Issuer"] = "corbel-tests",
            ["Jwt:Audience"] = "corbel-tests",
        }));

        builder.ConfigureTestServices(services =>
        {
            if (enableAuthRateLimit)
            {
                // Keep the production "auth" policy REAL so the 429 path can be asserted; only neutralize the
                // global per-user limiter so it can't interfere. Don't RemoveAll here — that would drop the auth
                // policy too. This Configure runs after the host's, so it just overrides the global limiter.
                services.Configure<RateLimiterOptions>(options =>
                    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                        _ => RateLimitPartition.GetNoLimiter("test")));
                return;
            }

            // Determinism (default): replace both production rate-limit policies with no-ops so a fast run can't trip 429.
            services.RemoveAll<IConfigureOptions<RateLimiterOptions>>();
            services.Configure<RateLimiterOptions>(options =>
            {
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                    _ => RateLimitPartition.GetNoLimiter("test"));
                options.AddPolicy("auth", _ => RateLimitPartition.GetNoLimiter("test"));
            });
        });
    }

    /// <summary>A client that surfaces redirects/errors as-is instead of silently following them.</summary>
    public HttpClient CreateApiClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    /// <summary>
    /// A cookie-mode client whose cookies are tracked by a hand-rolled <see cref="CookieJar"/> (rather than a real
    /// CookieContainer, which would withhold the Secure cookies over the http test transport). The jar exposes the
    /// readable XSRF-TOKEN so a test can echo it into the <c>X-XSRF-TOKEN</c> header and exercise the double-submit
    /// check. The handler is wired in front of the in-memory server with no auto-redirect/auto-cookie handling.
    /// </summary>
    public CookieSession CreateCookieClient()
    {
        var jar = new CookieJar();
        return new CookieSession(CreateDefaultClient(jar), jar);
    }

    /// <summary>Provisions a confirmed user directly via Identity (bypasses the mailed confirmation link).</summary>
    public async Task EnsureConfirmedUserAsync(string email, string password)
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        if (await users.FindByEmailAsync(email) is not null)
            return;

        var user = new AppUser { Id = Guid.CreateVersion7(), UserName = email, Email = email, EmailConfirmed = true };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Could not create test user '{email}': {string.Join("; ", result.Errors.Select(e => e.Description))}");
    }

    /// <summary>Logs in via the real auth endpoint and returns the token envelope.</summary>
    public async Task<TokenResponse> LoginAsync(string email, string password)
    {
        using var client = CreateApiClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TokenResponse>(TestJson.Options))!;
    }

    /// <summary>Ensures a confirmed user, logs in, and returns a client with the Bearer token attached.</summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(string email, string password)
    {
        await EnsureConfirmedUserAsync(email, password);
        var token = await LoginAsync(email, password);

        var client = CreateApiClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return client;
    }

    /// <summary>Ensures a confirmed user that is a member of <paramref name="role"/> (the role is seeded by the fixture).</summary>
    public async Task EnsureUserInRoleAsync(string email, string password, string role)
    {
        await EnsureConfirmedUserAsync(email, password);
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await users.FindByEmailAsync(email)
                   ?? throw new InvalidOperationException($"No user '{email}' to grant '{role}'.");
        if (!await users.IsInRoleAsync(user, role))
            await users.AddToRoleAsync(user, role);
    }

    /// <summary>Ensures an Admin user, logs in, and returns a Bearer client (for exercising Admin-only endpoints).</summary>
    public async Task<HttpClient> CreateAdminClientAsync(string email, string password)
    {
        await EnsureUserInRoleAsync(email, password, AppRoles.Admin);
        var token = await LoginAsync(email, password);

        var client = CreateApiClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return client;
    }
}
