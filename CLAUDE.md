# Working in Corbel

A .NET 10 vertical-slice minimal API + React 19/Vite SPA. This file is the orientation for anyone (human or
AI) editing the code; the [README](README.md) covers setup and how to run it.

## Commands

```bash
just test          # backend tests (xUnit + Testcontainers; needs Docker)
dotnet build Corbel.slnx
pnpm -C web test    # SPA tests (Vitest + MSW)
pnpm -C web lint    # Biome (format + lint); lint:boundaries runs the ESLint architecture rules
just gen-client    # regenerate openapi.json + web/src/types/schema.d.ts (CI fails if these drift)
```

CI promotes warnings to errors (`-p:ContinuousIntegrationBuild=true`) and runs `dotnet format
--verify-no-changes`, so build clean and format before pushing.

## Backend conventions

- **A feature is one file** under `src/Corbel.Api/Features/<Area>/<Action>.cs`: the command/query, its
  FluentValidation validator, the handler, and the `IEndpoint`. Endpoints are auto-discovered (no central
  registration); handlers are wired by the Mediator source generator.
- **Writes** implement `IRequest<T>` + `IWriteCommand` so `TransactionBehavior` wraps them in one
  transaction. **Queries** are plain `IRequest<T>`. `ValidationBehavior` runs first.
- **Errors are exceptions** → `GlobalExceptionHandler` → RFC 9457 ProblemDetails. Throw `AppException`
  subclasses (`NotFoundException`/`ForbiddenException`/`UnauthorizedException`, which carry an HTTP status) or
  `DomainException` (always 422). Every error carries a machine-readable `errorCode` from
  `Common/Errors/ErrorCodes.cs` — add a constant there and mirror it in `web/src/types/api.ts`.
- **Ownership** is enforced inside each slice's query predicate; a resource you don't own returns 404, not 403.
- **Domain events**: raise them from an aggregate behavior method (`Note.Archive()`); `TransactionBehavior`
  publishes them once, after the command commits, off the still-tracked entities. Always save via
  `SaveChangesAsync`.
- **Persistence**: PostgreSQL, snake_case, `xmin` optimistic concurrency (→ 409). `DbContext.Remove` on an
  `ISoftDelete` entity is turned into a soft-delete update by the auditing interceptor.
- The Domain layer must not reference Infrastructure, Features, EF Core, or ASP.NET — the architecture tests
  enforce this, plus that feature slices stay independent of each other.

## Frontend conventions

- **bulletproof-react** layering, enforced by ESLint (`import/no-restricted-paths`): `shared
  (components/hooks/lib/config/types) → features → app`. Add an import-boundary zone per new feature.
- In components, get the React Query client with `useQueryClient()`; the imported singleton `queryClient` is
  only for non-React modules (the auth store, the cache callbacks).
- The typed API client is generated from the backend's OpenAPI document — never hand-edit
  `web/src/types/schema.d.ts`; run `just gen-client` after a contract change.

## Conventions

Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/).
