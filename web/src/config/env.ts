import { z } from 'zod';

/**
 * Typed, centralized access to the public build-time config. Everything here is baked into the bundle
 * and visible to users — never read a secret through this object. Validated with Zod so a missing or
 * malformed var fails fast at module load instead of surfacing as a silent `undefined` deep in the app.
 */
const schema = z.object({
  VITE_APP_NAME: z.string().default('Corbel'),
  /**
   * Base URL for the typed API client. Empty by default: the generated OpenAPI paths already include the
   * `/api` prefix, and the dev proxy / nginx route `/api` to the backend on the same origin — so a non-empty
   * base would double-prefix to `/api/api/...`. Set only to target a different host.
   */
  VITE_API_BASE_URL: z.string().default(''),
  /** Empty string keeps the error reporter as the no-op default (see lib/monitoring). */
  VITE_SENTRY_DSN: z.string().default(''),
  VITE_SENTRY_ENVIRONMENT: z.string().default(import.meta.env.MODE),
  /** Wired to the build SHA/tag in CI for release-tagged events + sourcemap matching. */
  VITE_APP_VERSION: z.string().default('dev'),
});

const parsed = schema.safeParse(import.meta.env);
if (!parsed.success) {
  throw new Error(`Invalid environment configuration: ${parsed.error.message}`);
}

export const env = {
  appName: parsed.data.VITE_APP_NAME,
  apiBaseUrl: parsed.data.VITE_API_BASE_URL,
  sentryDsn: parsed.data.VITE_SENTRY_DSN,
  sentryEnvironment: parsed.data.VITE_SENTRY_ENVIRONMENT,
  appVersion: parsed.data.VITE_APP_VERSION,
} as const;
