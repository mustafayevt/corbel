import { create } from 'zustand';
import { monitoring } from '@/lib/monitoring';
import { queryClient } from '@/lib/react-query';

/** Minimal identity kept client-side for the navbar only. Seeded from JWT claims (see `decodeUser`) and
 *  confirmed against `GET /api/auth/me` on boot; never trusted for authorization — the API re-checks every
 *  request. */
export interface AuthUser {
  id?: string;
  email: string;
  displayName?: string | null;
}

interface AuthState {
  /** Short-lived JWT, held ONLY in memory (never localStorage) to limit XSS blast radius. */
  accessToken: string | null;
  user: AuthUser | null;
  /** Flips to true once the one-shot silent refresh on boot has resolved (success or failure), so
   *  guards can wait instead of bouncing an authenticated user to /login on a hard reload. */
  hasBootstrapped: boolean;

  setSession: (accessToken: string, user?: AuthUser | null) => void;
  setUser: (user: AuthUser | null) => void;
  markBootstrapped: () => void;
  logout: () => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  accessToken: null,
  user: null,
  hasBootstrapped: false,

  setSession: (accessToken, user) =>
    set((state) => ({
      accessToken,
      user: user === undefined ? state.user : user,
      hasBootstrapped: true,
    })),

  setUser: (user) => set({ user }),

  markBootstrapped: () => set({ hasBootstrapped: true }),

  logout: () => {
    set({ accessToken: null, user: null, hasBootstrapped: true });
    // Drop every cached query so a different user can't see the previous one's data.
    queryClient.clear();
    monitoring.setUser(null);
  },
}));
