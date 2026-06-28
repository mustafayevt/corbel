# Corbel task runner. Run `just` (or `just --list`) to see every recipe.
# Requires: just, .NET 10 SDK, Node 22 + pnpm (via corepack), Docker.

set shell := ["bash", "-uc"]
set dotenv-load := false

api := "src/Corbel.Api"
apphost := "src/Corbel.AppHost"
solution := "Corbel.slnx"
web := "web"

# Default recipe: list everything.
default:
    @just --list

# Restore the local .NET tools (dotnet-ef) pinned in .config/dotnet-tools.json.
tools:
    dotnet tool restore

# One-time local setup: restore tools, trust the dev HTTPS cert, put a random JWT key + dev admin password
# into user-secrets (no secrets ship in the repo), and create .env.
bootstrap: tools
    dotnet dev-certs https --trust
    @if dotnet user-secrets list --project {{api}} 2>/dev/null | grep -q '^Jwt:SigningKey'; then \
        echo "Jwt:SigningKey already set in user-secrets; leaving it."; \
    else \
        dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)" --project {{api}} && \
        echo "Generated a new Jwt:SigningKey in user-secrets for {{api}}."; \
    fi
    @if dotnet user-secrets list --project {{api}} 2>/dev/null | grep -q '^Seed:AdminPassword'; then \
        echo "Seed:AdminPassword already set in user-secrets; leaving it."; \
    else \
        pw="$(openssl rand -base64 24)Aa1!" && \
        dotnet user-secrets set "Seed:AdminPassword" "$pw" --project {{api}} && \
        echo "Dev admin will be seeded on first run — login: admin@corbel.local / $pw"; \
    fi
    @if [ -f .env ]; then echo ".env already exists; leaving it."; else cp .env.example .env && echo "Created .env from .env.example — edit it before 'just up'."; fi

# Run the full stack (API + Postgres + web) for the inner dev loop via .NET Aspire.
# (Aspire starts the Vite app only when pnpm is on PATH — true in a terminal. From an IDE whose PATH lacks
# Node, run the SPA separately with `just web`.)
dev: bootstrap
    dotnet run --project {{apphost}}

# Run just the SPA dev server (http://localhost:5173), proxying /api to the API on :7080.
# Use this when the API/Postgres are running (via `just dev` or the IDE) but the SPA isn't orchestrated.
web:
    pnpm -C {{web}} install
    pnpm -C {{web}} dev

# Apply pending EF Core migrations to the database.
migrate: tools
    dotnet ef database update --project {{api}}

# Create a new EF Core migration, e.g. `just add-migration AddWidgets`.
add-migration name: tools
    dotnet ef migrations add {{name}} --project {{api}} --output-dir Infrastructure/Persistence/Migrations

# Regenerate the typed frontend client from the API's live OpenAPI document.
# Boots the API (needs a reachable Postgres — start one first via `just dev` or `docker compose up -d postgres`),
# curls /openapi/v1.json into ./openapi.json, then runs the openapi-typescript generator over it.
gen-client:
    #!/usr/bin/env bash
    set -euo pipefail
    export ASPNETCORE_ENVIRONMENT=Development
    export Jwt__SigningKey="${Jwt__SigningKey:-$(openssl rand -base64 48)}"
    echo "Booting the API to dump its OpenAPI document..."
    dotnet run --project {{api}} --urls http://localhost:5229 >/tmp/corbel-gen-client.log 2>&1 &
    api_pid=$!
    trap 'kill "$api_pid" 2>/dev/null || true' EXIT
    for _ in $(seq 1 90); do
        if curl -fsS http://localhost:5229/openapi/v1.json -o openapi.json; then echo "Wrote openapi.json"; break; fi
        kill -0 "$api_pid" 2>/dev/null || { echo "API exited early — see /tmp/corbel-gen-client.log" >&2; exit 1; }
        sleep 1
    done
    [ -s openapi.json ] || { echo "Failed to fetch openapi.json (is Postgres reachable?)" >&2; exit 1; }
    pnpm -C {{web}} run gen:client
    echo "Regenerated web/src/types/schema.d.ts from openapi.json — review with 'git diff'."

# Run the backend test suite.
test:
    dotnet test {{solution}}

# Build and run the production-like container stack (reads .env).
up:
    docker compose up --build

# Rename the template from "Corbel" to a new name, e.g. `just rename Acme`.
# Rewrites BOTH the PascalCase token (Corbel -> Acme) and the lowercase token (corbel -> acme: npm package
# name, db/service names, JWT issuer/audience, OTel service name, the "corbel-api" user-secrets id) inside
# text files, then renames matching files and folders. Use a simple alphanumeric name. Review with `git diff`.
rename NEW:
    #!/usr/bin/env bash
    set -euo pipefail
    old="Corbel"; old_lower="corbel"
    new="{{NEW}}"; new_lower="$(echo "{{NEW}}" | tr '[:upper:]' '[:lower:]')"
    if [ "$new" = "$old" ]; then echo "Project is already named '$old'. Nothing to do."; exit 0; fi
    echo "Renaming '$old' -> '$new' (and '$old_lower' -> '$new_lower') ..."
    # 1) Replace both tokens inside text files (skip binaries, build output, VCS and dependencies).
    grep -rlI --exclude-dir=.git --exclude-dir=bin --exclude-dir=obj \
              --exclude-dir=node_modules --exclude-dir=dist --exclude-dir=.idea --exclude-dir=.vs \
              -e "$old" -e "$old_lower" . \
      | while IFS= read -r f; do
          LC_ALL=C sed -i.bak -e "s/$old/$new/g" -e "s/$old_lower/$new_lower/g" "$f"
          rm -f "$f.bak"
        done
    # 2) Rename files and folders whose path contains either token (deepest paths first).
    find . -depth \( -name "*$old*" -o -name "*$old_lower*" \) \
         -not -path '*/.git/*' -not -path '*/bin/*' -not -path '*/obj/*' \
         -not -path '*/node_modules/*' -not -path '*/dist/*' -not -path '*/.idea/*' \
      | while IFS= read -r p; do
          np="$(dirname "$p")/$(basename "$p" | sed -e "s/$old/$new/g" -e "s/$old_lower/$new_lower/g")"
          [ "$p" != "$np" ] && mv "$p" "$np"
        done
    echo "Done. Review with 'git diff', then run: just bootstrap && dotnet build && just test"
