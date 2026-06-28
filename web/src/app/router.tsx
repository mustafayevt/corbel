import { type ComponentType, lazy, Suspense } from 'react';
import { createBrowserRouter, Outlet } from 'react-router-dom';
import { NotFound } from '@/components/errors/not-found';
import { RouteErrorBoundary } from '@/components/errors/route-error-boundary';
import { Spinner } from '@/components/ui/spinner';
import { paths } from '@/config/paths';
import { ProtectedRoute } from '@/features/auth/components/protected-route';
import { DashboardLayout } from './routes/app/dashboard-layout';

function RouteFallback() {
  return (
    <div className="flex min-h-svh items-center justify-center">
      <Spinner className="size-6 text-muted-foreground" label="Loading" />
    </div>
  );
}

/** Wrap a lazy route module (exporting `Component`) in Suspense so each page is its own chunk. */
function lazyEl(loader: () => Promise<{ Component: ComponentType }>) {
  const LazyComponent = lazy(async () => ({ default: (await loader()).Component }));
  return (
    <Suspense fallback={<RouteFallback />}>
      <LazyComponent />
    </Suspense>
  );
}

export const router = createBrowserRouter([
  {
    // Pathless root layout. Its errorElement is the single branded boundary for any render/loader error a
    // descendant route throws, so uncaught errors never reach React Router's raw default screen (and a
    // thrown error renders as one full-page ErrorState rather than nested inside the dashboard chrome).
    element: <Outlet />,
    errorElement: <RouteErrorBoundary />,
    children: [
      {
        path: paths.login.path,
        element: lazyEl(() => import('./routes/login-route')),
      },
      {
        path: paths.register.path,
        element: lazyEl(() => import('./routes/register-route')),
      },
      {
        // Everything below requires a session. The guard waits for the on-boot silent refresh, then either
        // renders the authenticated shell (with the routed page) or redirects to /login with a returnTo.
        element: <ProtectedRoute />,
        children: [
          {
            element: <DashboardLayout />,
            children: [
              {
                index: true,
                element: lazyEl(() => import('./routes/notes-route')),
              },
              // Static `/notes/new` outranks the dynamic `/notes/:id` by React Router's specificity ranking.
              {
                path: paths.notes.new.path,
                element: lazyEl(() => import('./routes/note-editor-route')),
              },
              {
                path: paths.notes.detail.path,
                element: lazyEl(() => import('./routes/note-editor-route')),
              },
            ],
          },
        ],
      },
      // Branded 404 for any unmatched URL.
      { path: '*', element: <NotFound /> },
    ],
  },
]);
