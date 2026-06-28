import { MutationCache, QueryCache, QueryClient } from '@tanstack/react-query';
import createClient from 'openapi-react-query';
import { notificationsStore } from '@/components/ui/notifications/notifications-store';
import { client } from '@/lib/api-client';
import { monitoring } from '@/lib/monitoring';
import { asProblem, getErrorMessage } from '@/lib/problem';
import type { ProblemDetails } from '@/types/api';

/** A 401 is owned by the transport (silent refresh, else hard logout); our own toast would double-message it. */
function isTerminalAuth(error: unknown): boolean {
  return asProblem(error)?.status === 401;
}

function report(error: unknown, context: Record<string, unknown>) {
  const problem = asProblem(error);
  if (problem?.traceId) {
    context.traceId = problem.traceId; // correlate the FE error to the BE log line
  }
  monitoring.captureException(error, context);
}

/**
 * Build a QueryClient wired with the centralized cache error handling. Exported as a factory so tests can
 * construct a client that exercises the exact same toast/report behavior as production (the singleton below).
 */
export function createQueryClient(): QueryClient {
  return new QueryClient({
    queryCache: new QueryCache({
      onError: (error, query) => {
        report(error, { kind: 'query', queryKey: query.queryKey });
        // Toast only a BACKGROUND refetch failure: a first-load error (data === undefined) is rendered inline
        // by the component, so toasting it too would double-surface.
        if (isTerminalAuth(error) || query.state.data === undefined) {
          return;
        }
        if (query.meta?.errorMessage === false) {
          return;
        }
        notificationsStore.getState().add({
          variant: 'error',
          title: 'Could not refresh data',
          description: getErrorMessage(error, 'Please try again.'),
        });
      },
    }),
    mutationCache: new MutationCache({
      onError: (error, _variables, _context, mutation) => {
        report(error, { kind: 'mutation' });
        if (isTerminalAuth(error)) {
          return;
        }
        // Forms that map ProblemDetails onto RHF fields set meta.errorMessage=false to avoid a double toast.
        const meta = mutation.options.meta;
        if (meta?.errorMessage === false) {
          return;
        }
        notificationsStore.getState().add({
          variant: 'error',
          title: 'Action failed',
          description: getErrorMessage(
            error,
            typeof meta?.errorMessage === 'string' ? meta.errorMessage : 'Something went wrong.',
          ),
        });
      },
    }),
    defaultOptions: {
      queries: {
        // openapi-react-query rejects with the parsed ProblemDetails body, which carries `status`.
        // Never retry a client error (4xx) — it will just fail again; do retry transient 5xx/network.
        retry: (failureCount, error) => {
          const status = (error as ProblemDetails | undefined)?.status;
          if (typeof status === 'number' && status >= 400 && status < 500) {
            return false;
          }
          return failureCount < 2;
        },
        staleTime: 30_000,
        gcTime: 5 * 60_000,
        networkMode: 'online',
        refetchOnWindowFocus: false,
        refetchOnReconnect: true,
      },
      mutations: {
        retry: false,
        networkMode: 'online',
      },
    },
  });
}

/** The single React Query cache, shared app-wide. Lives in `lib/` with no dependency on any feature. */
export const queryClient = createQueryClient();

/**
 * Typed React Query bindings over the openapi-fetch `client`. Usage:
 *   $api.useQuery('get', '/api/notes', { params: { query: { page } } })
 *   $api.useMutation('post', '/api/notes')
 * Method + path + params + response shapes are all checked against `paths` in types/schema.d.ts.
 */
export const $api = createClient(client);
