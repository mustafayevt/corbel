var builder = DistributedApplication.CreateBuilder(args);

var corbel = builder.AddPostgres("postgres")
    .WithImageTag("17-alpine")   // parity with docker-compose, so dev and prod run the same Postgres major
    .WithDataVolume("corbel-pgdata")
    // Named volume (not an auto-generated hash) so the dev DB is easy to find/remove. PGDATA points at a
    // subdirectory of the mounted volume so `initdb` never trips on a non-empty mount root (e.g. a lost+found
    // or a half-initialized cluster from an interrupted run) — the standard postgres-on-a-volume hardening.
    .WithEnvironment("PGDATA", "/var/lib/postgresql/data/pgdata")
    .AddDatabase("corbel");

// Dev inner-loop JWT signing key so the API boots on F5 / `dotnet run` with zero setup — without committing a
// secret. It's regenerated each run (dev sessions don't outlive a restart); the container stack and production
// supply Jwt__SigningKey from .env / a secret manager instead.
var devJwtSigningKey = $"{Guid.NewGuid():N}{Guid.NewGuid():N}";

// Pin the https launch profile so api.GetEndpoint("https") (used for the Vite dev proxy) resolves deterministically.
var api = builder.AddProject<Projects.Corbel_Api>("api", launchProfileName: "https")
    .WithReference(corbel)
    .WaitFor(corbel)
    .WithEnvironment("Jwt__SigningKey", devJwtSigningKey)
    // Pin the dev HTTPS port so the SPA's proxy target is stable (https://localhost:7080) whether the SPA is
    // orchestrated by Aspire or run standalone via `just web`. Without this Aspire assigns a random port and
    // a standalone `pnpm dev` (which proxies /api to :7080) can't reach the API.
    .WithEndpoint("https", endpoint => endpoint.Port = 7080);

// Orchestrate the Vite dev server ONLY when a Node/pnpm toolchain is on the PATH this process inherits. IDEs
// launched from the macOS Dock/Finder don't get your shell PATH (Homebrew/nvm/fnm live outside the default GUI
// PATH), so unconditionally adding it would make the whole run fail with "web installer failed to start". When
// it's skipped you run the SPA yourself with `just web` (`pnpm -C web dev`), which proxies /api to the API.
if (IsExecutableOnPath("pnpm"))
{
    builder.AddViteApp("web", "../../web")
        .WithPnpm()   // AddViteApp defaults to npm, which breaks on this project's pnpm lockfile/node_modules
        .WithReference(api)
        .WithEnvironment("VITE_DEV_PROXY_TARGET", api.GetEndpoint("https"))
        .WaitFor(api);
}
else
{
    Console.WriteLine(
        "[corbel] pnpm was not found on PATH — skipping the Vite app. Run the SPA with `just web` " +
        "(or `pnpm -C web dev`); it proxies /api to https://localhost:7080.");
}

builder.Build().Run();

// True when <paramref name="executable"/> is resolvable on the current PATH (so resources that shell out to a
// package manager don't fail to start when the toolchain isn't visible to the host process).
static bool IsExecutableOnPath(string executable)
{
    var path = Environment.GetEnvironmentVariable("PATH");
    if (string.IsNullOrEmpty(path))
        return false;

    string[] candidates = OperatingSystem.IsWindows()
        ? [executable + ".cmd", executable + ".exe", executable]
        : [executable];

    return path.Split(Path.PathSeparator)
        .Where(directory => !string.IsNullOrEmpty(directory))
        .Any(directory => candidates.Any(name => File.Exists(Path.Combine(directory, name))));
}
