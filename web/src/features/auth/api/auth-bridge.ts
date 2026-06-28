import { decodeUser } from '@/features/auth/api/decode-user';
import { useAuthStore } from '@/features/auth/stores/auth-store';
import { client, setAuthBridge } from '@/lib/api-client';

/**
 * One refresh attempt. Goes through `client`, so onRequest attaches the CSRF header and the
 * `/api/auth/refresh` path is guarded in onResponse — a 401 here will NOT recurse into another refresh.
 * On success it persists the rotated token; on failure the session is unrecoverable, so hard logout.
 */
async function performRefresh(): Promise<boolean> {
  const { data, error } = await client.POST('/api/auth/refresh', {});
  if (error || !data) {
    useAuthStore.getState().logout();
    return false;
  }
  useAuthStore.getState().setSession(data.accessToken, decodeUser(data.accessToken) ?? undefined);
  return true;
}

/**
 * Wire the shared api-client to the auth feature: this is the seam that keeps `lib/api-client` free of any
 * feature import. Call once at startup (app bootstrap) and in test setup.
 */
export function registerAuthBridge(): void {
  setAuthBridge({
    getAccessToken: () => useAuthStore.getState().accessToken,
    refresh: performRefresh,
  });
}
