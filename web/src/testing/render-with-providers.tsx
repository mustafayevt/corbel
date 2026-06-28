import type { QueryClient } from '@tanstack/react-query';
import { QueryClientProvider } from '@tanstack/react-query';
import { type RenderResult, render } from '@testing-library/react';
import type { ReactElement, ReactNode } from 'react';
import {
  createMemoryRouter,
  type DataRouter,
  MemoryRouter,
  type RouteObject,
  RouterProvider,
} from 'react-router-dom';
import { Notifications } from '@/components/ui/notifications/notifications';
import { ToastProvider } from '@/components/ui/toast';
import { ConfirmDialog } from '@/lib/confirm';
import { queryClient } from '@/lib/react-query';

/** Use the production singleton client so the imperative cache ops in feature code (auth-store logout, the
 *  React Query cache callbacks) and the components' context client are the same instance — exactly as in
 *  production. Isolation between tests comes from setup.ts clearing it in afterEach. */
function makeClient() {
  return queryClient;
}

function AllProviders({ client, children }: { client: QueryClient; children: ReactNode }) {
  return (
    <QueryClientProvider client={client}>
      <ToastProvider>
        {children}
        <Notifications />
        <ConfirmDialog />
      </ToastProvider>
    </QueryClientProvider>
  );
}

/** Component-level render inside a MemoryRouter. */
export function renderWithProviders(
  ui: ReactElement,
  { route = '/' }: { route?: string } = {},
): RenderResult & { client: QueryClient } {
  const client = makeClient();
  return {
    client,
    ...render(
      <AllProviders client={client}>
        <MemoryRouter initialEntries={[route]}>{ui}</MemoryRouter>
      </AllProviders>,
    ),
  };
}

/** Routing-level render with a real data router (for ProtectedRoute / redirects / 404). */
export function renderWithRouter(
  routes: RouteObject[],
  { initialEntries = ['/'] }: { initialEntries?: string[] } = {},
): RenderResult & { client: QueryClient; router: DataRouter } {
  const client = makeClient();
  const router = createMemoryRouter(routes, { initialEntries });
  return {
    client,
    router,
    ...render(
      <AllProviders client={client}>
        <RouterProvider router={router} />
      </AllProviders>,
    ),
  };
}
