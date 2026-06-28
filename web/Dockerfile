# syntax=docker/dockerfile:1

# --- Build stage: compile the SPA to static assets --------------------------------------------------
FROM node:22-alpine AS build
WORKDIR /app
RUN corepack enable

# Install deps first for better layer caching. The lockfile is committed and required: --frozen-lockfile
# guarantees a reproducible install and fails fast if package.json and the lockfile disagree.
COPY package.json pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile

COPY . .
# Uses the committed src/types/schema.d.ts. Run `pnpm gen:client` before building if you want the live spec.
# Strip the hidden source maps from the runtime artifact so nginx never serves them (upload them to your error
# tracker in CI first if you want symbolicated traces). The .gz/.br precompressed assets are kept.
RUN pnpm build && find dist -name '*.map' -delete

# --- Runtime stage: serve static files via nginx (rootless) -----------------------------------------
# nginx-unprivileged listens on 8080 and runs as the non-root "nginx" user (uid 101) out of the box,
# with its pid/temp paths under /tmp — no writable root-owned dirs required.
# Pinned to the 1.27 minor line (nginx's major is always 1, so this mirrors the major pins on node/postgres);
# Renovate bumps it.
FROM nginxinc/nginx-unprivileged:1.27-alpine AS runtime
# Overwrite the image's default server block. COPY works regardless of the active non-root USER, whereas
# a `RUN rm` of the root-owned default would fail with permission denied as uid 101.
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=3s CMD wget -qO- http://127.0.0.1:8080/ >/dev/null 2>&1 || exit 1
