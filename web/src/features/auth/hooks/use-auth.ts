import { useCallback } from 'react';
import { useAuthStore } from '@/features/auth/stores/auth-store';
import { client } from '@/lib/api-client';

/** Ergonomic auth surface for components: current identity, a boolean flag, and a logout that also
 *  clears the server refresh cookie. */
export function useAuth() {
  const user = useAuthStore((state) => state.user);
  const accessToken = useAuthStore((state) => state.accessToken);
  const storeLogout = useAuthStore((state) => state.logout);

  const logout = useCallback(async () => {
    try {
      // Best-effort revoke of the httpOnly refresh cookie. We log out locally regardless of the result.
      await client.POST('/api/auth/logout', {});
    } catch {
      // ignore network/server errors during logout
    } finally {
      storeLogout();
    }
  }, [storeLogout]);

  return { user, isAuthenticated: accessToken !== null, logout };
}
