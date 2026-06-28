import { QueryErrorResetBoundary } from '@tanstack/react-query';
import { isRouteErrorResponse, Link, useNavigate, useRouteError } from 'react-router-dom';
import { Button, buttonVariants } from '@/components/ui/button';
import { useDocumentTitle } from '@/hooks/use-document-title';
import { asProblem, getErrorMessage } from '@/lib/problem';
import { ErrorState } from './error-state';

const FALLBACK_MESSAGE = 'An unexpected error interrupted the app. Please try again.';

/**
 * The router-level `errorElement`: a single branded screen for any render/loader error a descendant
 * route throws. Wrapped in `QueryErrorResetBoundary` so "Try again" clears failed query state before the
 * route re-runs. We surface a ProblemDetails message when there is one, but never echo a raw render
 * error to users — those get a generic message instead of a stack trace.
 */
export function RouteErrorBoundary() {
  const error = useRouteError();
  const navigate = useNavigate();

  // Render/loader errors only — unmatched URLs are handled by the catch-all `*` route (<NotFound/>), so this
  // boundary never owns a 404 and stays a single generic "something went wrong" screen.
  const routeError = isRouteErrorResponse(error) ? error : null;
  const problem = asProblem(error);
  const description = problem ? getErrorMessage(error, FALLBACK_MESSAGE) : FALLBACK_MESSAGE;

  useDocumentTitle('Something went wrong');

  return (
    <QueryErrorResetBoundary>
      {({ reset }) => (
        <ErrorState
          status={routeError?.status}
          title="Something went wrong"
          description={description}
          traceId={problem?.traceId ?? undefined}
        >
          <Button
            onClick={() => {
              // Clear any failed query state so it refetches, then re-run the current route. React Router
              // only resets a route error boundary on a location change, so history.go(0) is the reliable
              // way to recover from a render error and re-run its queries.
              reset();
              navigate(0);
            }}
          >
            Try again
          </Button>
          <Link to="/" className={buttonVariants({ variant: 'outline' })}>
            Back to home
          </Link>
        </ErrorState>
      )}
    </QueryErrorResetBoundary>
  );
}
