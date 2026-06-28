import { Component, type ErrorInfo, type ReactNode } from 'react';
import { Button } from '@/components/ui/button';
import { env } from '@/config/env';
import { ErrorState } from './error-state';

interface ErrorBoundaryProps {
  children: ReactNode;
}

interface ErrorBoundaryState {
  hasError: boolean;
}

/**
 * Last-resort boundary around the router. Catches synchronous render errors thrown while bootstrapping
 * the app shell — anything that escapes the route-level `errorElement`, such as provider setup or router
 * construction failing. Written as a class because React error boundaries have no hook equivalent.
 */
export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  override state: ErrorBoundaryState = { hasError: false };

  static getDerivedStateFromError(): ErrorBoundaryState {
    return { hasError: true };
  }

  override componentDidCatch(error: unknown, info: ErrorInfo) {
    document.title = `Something went wrong · ${env.appName}`;
    // No telemetry pipeline in the core template; log so the failure is at least visible in dev tools.
    console.error('Unhandled application error:', error, info.componentStack);
  }

  override render() {
    if (this.state.hasError) {
      return (
        <ErrorState
          title="Something went wrong"
          description="The app ran into an unexpected problem. Reloading usually fixes it."
        >
          <Button onClick={() => window.location.reload()}>Reload the app</Button>
        </ErrorState>
      );
    }
    return this.props.children;
  }
}
