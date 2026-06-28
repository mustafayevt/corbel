import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { Spinner } from '@/components/ui/spinner';
import { paths } from '@/config/paths';
import { useAuthStore } from '@/features/auth/stores/auth-store';

/**
 * Route guard for authenticated areas. While the on-boot silent refresh is still resolving we hold a
 * splash (so a reload doesn't flash the login page); afterwards, an unauthenticated user is redirected
 * to /login with a `returnTo` so they land back where they started.
 */
export function ProtectedRoute() {
  const accessToken = useAuthStore((state) => state.accessToken);
  const hasBootstrapped = useAuthStore((state) => state.hasBootstrapped);
  const location = useLocation();

  if (!hasBootstrapped) {
    return (
      <div className="flex min-h-svh items-center justify-center">
        <Spinner className="size-6 text-muted-foreground" label="Loading your session" />
      </div>
    );
  }

  if (!accessToken) {
    return <Navigate to={paths.login.getHref(`${location.pathname}${location.search}`)} replace />;
  }

  return <Outlet />;
}
