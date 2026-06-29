# Corbel

[![CI](https://github.com/your-org/corbel/actions/workflows/ci.yml/badge.svg)](https://github.com/your-org/corbel/actions/workflows/ci.yml) [![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE) ![.NET 10](https://img.shields.io/badge/.NET-10-512BD4) ![Node 22](https://img.shields.io/badge/Node-22-339933)

A starter template for **.NET 10 + React** apps. Corbel pairs a vertical-slice ASP.NET Core minimal API with a
typed React SPA and wires up the boring-but-essential parts — auth, validation, observability, migrations,
containers and CI — so the first thing you write is a feature, not plumbing.

## Why Corbel

Most starters force a trade-off: a thin scaffold you outgrow in a week, or a heavyweight framework you
spend days un-wiring. Corbel aims for the middle — the decisions a senior team would make anyway, made once
and documented: atomic refresh-token rotation with reuse detection, Argon2id, signed double-submit CSRF, a
single RFC 9457 error contract, optimistic concurrency, deny-by-default authorization, post-commit
domain-event dispatch (see `Note.Archive()` → `NoteArchivedHandler`), a typed frontend client generated from
the live OpenAPI spec, and CI that actually gates what ships. Clone it, run `just rename`, and start on
feature #1.

**Non-goals.** Corbel deliberately does *not* ship a component library, a CMS, multi-tenancy, an external
message/event bus, or cloud-specific infrastructure-as-code. It stays a focused, readable base you fully own
— extend it, don't fight it. Heavier options (Redis, OIDC/MFA) are left for you to add when you need them
rather than shipped by default.

## Stack

| Layer            | Choices                                                                                 |
| ---------------- | --------------------------------------------------------------------------------------- |
| Backend          | .NET 10, ASP.NET Core minimal APIs, vertical slices, [Mediator] (source-generated)      |
| Data             | EF Core 10 + PostgreSQL (Npgsql, snake_case), design-time migrations                    |
| Auth             | Dual-mode JWT (HS256) — same-origin cookie **or** bearer — refresh-token rotation, Argon2id |
| Validation / tx  | FluentValidation + a unit-of-work transaction, both as Mediator pipeline behaviors      |
| API surface      | OpenAPI 3.1 (served at runtime) + Scalar UI (dev), RFC 9457 ProblemDetails, security headers |
| Observability    | Serilog + OpenTelemetry (logs, metrics, traces) over OTLP                               |
| Frontend         | React + Vite + TypeScript, a typed client generated from the API's OpenAPI spec          |
| Orchestration    | .NET Aspire (dev inner loop) and Docker Compose (production-like)                        |
| Tests            | xUnit v3, Testcontainers (PostgreSQL), NetArchTest                                       |

[Mediator]: https://github.com/martinothamar/Mediator

## Prerequisites

Four tools. Install commands per platform:

| Tool                                                                | Windows (`winget`)                       | macOS (Homebrew)                  | Linux                                                              |
| ------------------------------------------------------------------- | ---------------------------------------- | --------------------------------- | ----------------------------------------------------------------- |
| [.NET 10 SDK](https://dotnet.microsoft.com/download) (10.0.301+)    | `winget install Microsoft.DotNet.SDK.10` | `brew install --cask dotnet-sdk`  | [Microsoft docs](https://learn.microsoft.com/dotnet/core/install/linux) |
| [Node 22](https://nodejs.org/) + [pnpm](https://pnpm.io/)           | `winget install OpenJS.NodeJS.LTS`       | `brew install node@22`            | [nodejs.org](https://nodejs.org/) or `nvm`                        |
| [Docker](https://www.docker.com/) (Postgres, Testcontainers, `up`)  | [Docker Desktop](https://www.docker.com/products/docker-desktop/) | `brew install --cask docker` | [Docker Engine](https://docs.docker.com/engine/install/)          |
| [just](https://github.com/casey/just) command runner                | `winget install Casey.Just`              | `brew install just`               | [package / prebuilt binary](https://github.com/casey/just#packages) |

After installing Node, turn on pnpm with `corepack enable`. `just` is the recommended entry point — every
workflow is a recipe — and it runs natively on all three OSes (on Windows it drives the recipes through
PowerShell, so no Git Bash or WSL is needed). Each recipe is a thin wrapper you can also run by hand.

## Quickstart

```bash
just bootstrap && just dev
```

`bootstrap` restores the local tools, trusts the HTTPS dev cert, generates a random JWT signing key **and**
a dev admin password into `dotnet user-secrets` (no secret ships in the repo), and creates a `.env`. `dev`
launches the **.NET Aspire** AppHost, which starts PostgreSQL, the API and the Vite dev server together and
opens the Aspire dashboard (logs/traces/metrics for every service).

> **Running from an IDE (Rider / Visual Studio), esp. on macOS?** GUI apps launched from the Dock don't
> inherit your shell `PATH`, so the AppHost may not see `pnpm`/`node`. That's handled: Aspire then **skips the
> SPA** (no error) and still runs Postgres + the API + dashboard — start the SPA yourself with `just web`
> (`pnpm -C web dev`), which proxies `/api` to the API. In a terminal (`just dev`) pnpm is on PATH, so the SPA
> is orchestrated too. (To have the IDE orchestrate everything, put Node/pnpm on the IDE's PATH.)

No `just`? The equivalent — note you must supply a signing key yourself, because none is committed:

```bash
dotnet tool restore
dotnet dev-certs https --trust
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)" --project src/Corbel.Api
dotnet user-secrets set "Seed:AdminPassword" "ChangeMe_Admin1!" --project src/Corbel.Api   # optional dev admin
dotnet run --project src/Corbel.AppHost
```

### Run the API without Aspire

Aspire is a convenience, not a requirement. Start a Postgres yourself and run the API directly:

```bash
docker run --rm -p 5432:5432 -e POSTGRES_PASSWORD=postgres -e POSTGRES_USER=postgres -e POSTGRES_DB=corbel postgres:17-alpine
dotnet run --project src/Corbel.Api   # reads ConnectionStrings:corbel from appsettings.Development.json
```

### Production-like stack

```bash
cp .env.example .env   # then edit the CHANGE_ME values
just up                # docker compose up --build
```

This builds three containers — `postgres`, `api` (chiseled, rootless) and `web` (nginx serving the built
SPA) — and serves everything from **http://localhost:8080**.

## Container topology

`compose.yaml` at the repo root is the single container manifest; each `Dockerfile` lives next to the thing
it builds (`src/Corbel.Api/Dockerfile`, `web/Dockerfile` + `web/nginx.conf`). Only the **web** container
publishes a port (8080); nginx reverse-proxies `/api` → `api:8080`, so the browser only ever talks to one
same-origin host (no CORS, first-party cookies). The API waits for Postgres via `condition: service_healthy`
and applies migrations on startup (`Database:MigrateOnStartup`, default true; flip it off and migrate
out-of-process for serious deployments). The chiseled API image is shell-free, so it ships no container
`HEALTHCHECK`; nginx tolerates a briefly-unready API during boot.

## Adding a vertical slice

A feature is **one file** under `src/Corbel.Api/Features/<Area>/<Action>.cs` (flat — no per-action
subfolders). There is no central registration to edit: endpoints are discovered via `IEndpoint` and mapped
under the `/api` group, and handlers are wired by the Mediator source generator. A write slice holds four
small types:

```csharp
// Features/Widgets/CreateWidget.cs
public sealed record CreateWidgetCommand(string Name) : IRequest<WidgetResponse>, IWriteCommand; // IWriteCommand → wrapped in a transaction

public sealed class CreateWidgetValidator : AbstractValidator<CreateWidgetCommand>
{
    public CreateWidgetValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
}

public sealed class CreateWidgetHandler(AppDbContext db) : IRequestHandler<CreateWidgetCommand, WidgetResponse>
{
    public async ValueTask<WidgetResponse> Handle(CreateWidgetCommand command, CancellationToken cancellationToken)
    {
        // ... persist and return
    }
}

public sealed class CreateWidgetEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>            // app is the "/api" group
        app.MapPost("widgets", (CreateWidgetCommand cmd, ISender sender, CancellationToken ct) => sender.Send(cmd, ct))
            .WithName("CreateWidget").WithTags("Widgets")
            .WithSummary("Create a widget.")                         // flows into the OpenAPI doc + Scalar UI
            .RequireAuthorization();
}
```

Conventions: **commands** (writes) implement `IRequest<T>` + `IWriteCommand` so the `TransactionBehavior`
wraps them in one unit of work; **queries** are plain `IRequest<T>`. Validation runs automatically (the
`ValidationBehavior`). Bind the command directly as the body only when it has no route- or server-supplied
fields; otherwise use a `XxxRequest` body record and merge the route id (see `Features/Notes/UpdateNote.cs`).
`Features/Notes/` is a complete, copy-friendly reference (create / list / get / update / archive / delete);
`Features/Auth/` shows the auth slices and `Features/Admin/AdminPing.cs` shows a role-gated endpoint.

Document each endpoint with `.WithSummary()` / `.WithDescription()` — they flow into the OpenAPI document and the
Scalar UI. Richer per-operation metadata (parameter descriptions, request/response examples) and the document-wide
top matter (info, tags, auth schemes, the `errorCode` enum) live in the transformers under `src/Corbel.Api/Setup/`.

After changing API contracts, regenerate the typed frontend client (see `web/` for the generator):

```bash
just gen-client
```

## Database migrations

```bash
just add-migration AddWidgets   # scaffolds into src/Corbel.Api/Infrastructure/Persistence/Migrations
just migrate                    # apply pending migrations
```

`dotnet ef` reads the connection string from `ConnectionStrings__corbel` (with a localhost fallback for
design time). The committed migration is applied automatically at startup in dev and in the container stack.

## Making it your own

A full walkthrough from zero to a running app under your own name. The steps are identical on Windows, macOS
and Linux — only the tool-install commands differ ([Prerequisites](#prerequisites)).

**1. Install the toolchain.** .NET 10 SDK, Node 22 + pnpm, Docker and the `just` runner — per-OS commands are
in [Prerequisites](#prerequisites). Start Docker (Desktop on Windows/macOS) so Postgres and Testcontainers can
run, then confirm everything is on your PATH:

```bash
just --version && dotnet --version && node --version && pnpm --version && docker --version
```

**2. Get the code.** Click **Use this template** on GitHub (or clone), then enter the folder:

```bash
git clone <your-repo-url> && cd <your-repo>
```

**3. Rename it to your project.** This rewrites both the `Corbel` and `corbel` tokens across source, project,
solution and config files (npm package name, DB/service names, JWT issuer/audience, OTel service name, the
`corbel-api` user-secrets id), then renames the matching files and folders:

```bash
just rename Acme        # use a simple alphanumeric name
```

Review the result with `git diff`. Skipping this is fine too — the template runs as "Corbel" out of the box.

> **Windows:** `just rename` runs natively through PowerShell — no Git Bash or WSL. If you'd rather not install
> `just` just to rename, run the port directly:
> `powershell -NoProfile -ExecutionPolicy Bypass -File eng/windows/rename.ps1 Acme`.

**4. Fix the few identity bits a script can't infer:**

- [ ] `README.md` — replace `your-org` in the CI badge URLs (top of this file) with your GitHub `owner/repo`.
      (The OpenAPI contact URL in `src/Corbel.Api/Setup/DocumentInfoTransformer.cs` uses the same placeholder.)
- [ ] `SECURITY.md` — set the private security-contact address (the `security@example.com` placeholder).
- [ ] On GitHub, set the repo **About** blurb and topics (e.g. `dotnet`, `aspnetcore`, `react`, `vite`,
      `vertical-slice`, `template`) so others can discover it.

**5. Bootstrap and run.** `bootstrap` trusts the dev HTTPS cert, generates a random JWT key + a dev admin
password into user-secrets (no secret ships in the repo) and creates `.env`; `dev` launches the Aspire AppHost
(Postgres + API + SPA + dashboard):

```bash
just bootstrap && just dev
```

**6. Verify.** Build clean and run the tests (Testcontainers needs Docker running):

```bash
dotnet build && just test
```

That's it — a renamed, running app. Write feature #1: see [Adding a vertical slice](#adding-a-vertical-slice).

## Security & data notes

- **No secret ships in the repo.** `JwtOptions` validates on startup and the app refuses to boot without a
  real signing key of at least 32 characters. `just bootstrap` writes one into user-secrets for local dev; in
  CI/production it comes from your secret manager.
- **Dual-mode auth.** Browsers use an httpOnly, path-scoped refresh cookie + a signed double-submit CSRF
  token; the access token lives only in the response body (in memory on the SPA). Native/mobile clients use
  bearer mode (`useCookies: false`). Refresh tokens rotate with reuse detection (a replayed token revokes the
  whole family). All auth endpoints are rate-limited.
- **Authorization is deny-by-default.** A global fallback policy requires an authenticated user; endpoints opt
  out with `AllowAnonymous`. Per-object ownership is enforced in each slice (a note you don't own is a 404).
- **`.env` is gitignored**; `.env.example` holds only `CHANGE_ME` placeholders.
- **Health probes.** `/health/live` (liveness) and `/health/ready` (DB-connectivity readiness).

## Repository layout

```
src/
  Corbel.Api/             ASP.NET Core API — Domain, Features (slices), Infrastructure, Setup; Dockerfile
  Corbel.AppHost/         .NET Aspire orchestration (dev only)
  Corbel.ServiceDefaults/ Shared OpenTelemetry / health / resilience defaults
tests/
  Corbel.Api.Tests/       Unit + integration (Testcontainers) + architecture boundary tests
web/                      React + Vite SPA (typed API client) + Dockerfile + nginx.conf
compose.yaml              Production-like container stack
justfile                  Task runner (see `just --list`)
LICENSE                   MIT
```

## Contributing

Issues and PRs are welcome. Commit messages follow
[Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/); please report security issues privately
per [SECURITY.md](SECURITY.md).

`just bootstrap` also installs a Biome pre-commit hook (husky + lint-staged) that formats and lints staged files
(set it up on its own with a repo-root `pnpm install`). CI runs the same `dotnet format`, Biome and architecture
checks on every push, so the hook is a convenience — not a gate you can bypass.

## License

[MIT](LICENSE).
