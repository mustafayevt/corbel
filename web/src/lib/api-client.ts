import createClient, { type Middleware } from 'openapi-fetch';
import { env } from '@/config/env';
import type { paths } from '@/types/schema';

// Double-submit CSRF: the API sets a readable `XSRF-TOKEN` cookie; we echo it back in this header on the
// cookie-bearing auth calls (refresh/logout) so a cross-site form can't drive them. Names are an
// assumption the backend must match — see the report.
const CSRF_COOKIE = 'XSRF-TOKEN';
const CSRF_HEADER = 'X-XSRF-TOKEN';

// Sentinel so a retried request can never trigger a second refresh→retry cycle.
const RETRY_MARKER = 'X-Corbel-Retry';

/** The typed low-level client. `credentials: 'include'` so the httpOnly refresh cookie always rides along. */
export const client = createClient<paths>({
  baseUrl: env.apiBaseUrl,
  credentials: 'include',
  // Resolve the global fetch at call time instead of capturing it at module load, so a test double that
  // replaces globalThis.fetch after this module is imported (e.g. MSW's server.listen()) is honored.
  fetch: (request: Request) => fetch(request),
});

/**
 * The auth feature registers itself here so this shared transport layer never imports a feature — preserving
 * the unidirectional dependency flow (shared → features → app). `getAccessToken` feeds the bearer header and
 * the post-refresh replay; `refresh` performs exactly ONE refresh attempt (rotate the cookie + persist the
 * new token, or hard-logout) and resolves true iff a fresh token is now available.
 */
export interface AuthBridge {
  getAccessToken: () => string | null;
  refresh: () => Promise<boolean>;
}

let bridge: AuthBridge | null = null;

/** Wire the auth feature into the client. Called once at app startup (and in test setup). */
export function setAuthBridge(next: AuthBridge): void {
  bridge = next;
}

function pathOf(url: string): string {
  try {
    return new URL(url, 'http://localhost').pathname;
  } catch {
    return url;
  }
}

/** A 401 from any `/api/auth/*` route is an auth outcome (bad creds, dead session), not an expired
 *  access token — refreshing and retrying it would be wrong and could loop. */
function isAuthRoute(path: string): boolean {
  return path.startsWith('/api/auth/');
}

function readCookie(name: string): string | null {
  if (typeof document === 'undefined') {
    return null;
  }
  const escaped = name.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const match = document.cookie.match(new RegExp(`(?:^|; )${escaped}=([^;]*)`));
  return match?.[1] ? decodeURIComponent(match[1]) : null;
}

// --- Single-flight refresh ------------------------------------------------------------------------
// Many requests can 401 at once when an access token expires. We collapse them onto one in-flight
// refresh promise, so the cookie is rotated exactly once and every waiter shares the result. The actual
// rotation lives in the auth feature (the bridge); this only guarantees it runs at most once concurrently.
let refreshPromise: Promise<boolean> | null = null;

/** Returns true once a fresh access token is in the store; false after a hard logout (or with no bridge).
 *  Safe to call concurrently (used by both the 401 interceptor and the on-boot silent refresh). */
export function refreshToken(): Promise<boolean> {
  if (!bridge) {
    return Promise.resolve(false);
  }
  if (!refreshPromise) {
    const activeBridge = bridge;
    refreshPromise = activeBridge.refresh().finally(() => {
      refreshPromise = null;
    });
  }
  return refreshPromise;
}

// Cloned (body intact) at request time, so a retry after refresh can re-send POST/PUT bodies.
const retryClones = new WeakMap<Request, Request>();

const authMiddleware: Middleware = {
  onRequest({ request }) {
    const token = bridge?.getAccessToken() ?? null;
    if (token) {
      request.headers.set('Authorization', `Bearer ${token}`);
    }
    const path = pathOf(request.url);
    if (path === '/api/auth/refresh' || path === '/api/auth/logout') {
      const csrf = readCookie(CSRF_COOKIE);
      if (csrf) {
        request.headers.set(CSRF_HEADER, csrf);
      } else if (import.meta.env.DEV) {
        // Fail loudly in dev: a missing XSRF cookie means the double-submit header is silently omitted and
        // the backend will reject this cookie-auth call. Usually a CSRF_COOKIE name mismatch with the API.
        console.warn(
          `[api] expected the ${CSRF_COOKIE} cookie for ${path} but found none — the request will likely be rejected.`,
        );
      }
    }
    // Stash a pristine clone so onResponse can replay the exact request (with body) after refreshing.
    if (!request.headers.has(RETRY_MARKER)) {
      retryClones.set(request, request.clone());
    }
    return request;
  },

  async onResponse({ request, response }) {
    if (response.status !== 401) {
      return response;
    }
    const path = pathOf(request.url);
    if (isAuthRoute(path) || request.headers.has(RETRY_MARKER)) {
      return response;
    }

    const refreshed = await refreshToken();
    if (!refreshed) {
      return response; // hard logout already happened; surface the original 401
    }

    const original = retryClones.get(request);
    if (!original) {
      return response; // no pristine clone to replay (shouldn't happen) — surface the original 401
    }
    const replay = original.clone();
    replay.headers.set('Authorization', `Bearer ${bridge?.getAccessToken() ?? ''}`);
    replay.headers.set(RETRY_MARKER, '1');
    // Raw fetch on purpose: it bypasses this middleware, so the retry can't re-enter the refresh path.
    return fetch(replay);
  },
};

client.use(authMiddleware);
