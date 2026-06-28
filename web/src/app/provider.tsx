import { QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import type { ReactNode } from 'react';
import { ErrorBoundary } from '@/components/errors/error-boundary';
import { Notifications } from '@/components/ui/notifications/notifications';
import { ToastProvider } from '@/components/ui/toast';
import { ConfirmDialog } from '@/lib/confirm';
import { queryClient } from '@/lib/react-query';
import { ThemeProvider } from './theme-provider';

/**
 * The application-wide provider stack and single composition point for cross-cutting context.
 * Order: QueryClientProvider (every descendant shares one cache) > ThemeProvider (class toggling for the
 * whole tree) > ErrorBoundary (catches render/bootstrap errors below) > ToastProvider (so toasts render
 * on any route). Notifications + ConfirmDialog are store-driven singletons mounted beside the router.
 */
export function AppProvider({ children }: { children: ReactNode }) {
  return (
    <QueryClientProvider client={queryClient}>
      <ThemeProvider>
        <ErrorBoundary>
          <ToastProvider swipeDirection="right">
            {children}
            <Notifications />
            <ConfirmDialog />
          </ToastProvider>
        </ErrorBoundary>
      </ThemeProvider>
      {import.meta.env.DEV ? <ReactQueryDevtools initialIsOpen={false} /> : null}
    </QueryClientProvider>
  );
}
