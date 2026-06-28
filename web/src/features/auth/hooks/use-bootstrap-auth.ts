import { useEffect } from 'react';
import { useAuthStore } from '@/features/auth/stores/auth-store';
import { client, refreshToken } from '@/lib/api-client';
import { monitoring } from '@/lib/monitoring';

// Module-level guard so React 18/19 StrictMode's double-mount doesn't fire two refresh calls.
let bootstrapStarted = false;

/** Test-only: clears the one-shot guard so each test that mounts <App/> gets a fresh on-boot refresh. */
export function resetBootstrapAuthForTests(): void {
  bootstrapStarted = false;
}

/** Confirm the session against the server and cache the display identity (a token can outlive a disabled
 *  account, so we trust /me over the JWT claims). */
async function hydrateUser(): Promise<void> {
  const { data } = await client.GET('/api/auth/me');
  if (data) {
    useAuthStore.getState().setUser({
      id: data.id,
      email: data.email,
      displayName: data.displayName,
    });
    monitoring.setUser({ id: data.id, email: data.email });
  }
}

/**
 * Runs once on app load. With the access token held only in memory, a hard reload loses it — so we attempt a
 * single silent refresh from the httpOnly cookie to restore the session, then hydrate identity from
 * `GET /api/auth/me`. The store is marked bootstrapped either way; `ProtectedRoute` reads that flag from the
 * store directly, so this hook returns nothing (and doesn't subscribe its caller to store changes).
 */
export function useBootstrapAuth(): void {
  useEffect(() => {
    if (bootstrapStarted) {
      return;
    }
    bootstrapStarted = true;

    const { accessToken, markBootstrapped } = useAuthStore.getState();
    if (accessToken) {
      markBootstrapped();
      return;
    }

    // refreshToken() updates the store on success and hard-logs-out on failure (both set the flag);
    // the finally is a belt-and-suspenders guarantee the app never hangs on the boot splash.
    void refreshToken()
      .then((ok) => (ok ? hydrateUser() : undefined))
      .finally(() => useAuthStore.getState().markBootstrapped());
  }, []);
}
