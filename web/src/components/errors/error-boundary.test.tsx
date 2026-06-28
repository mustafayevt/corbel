import { render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderWithRouter } from '@/testing/render-with-providers';
import { ErrorBoundary } from './error-boundary';
import { RouteErrorBoundary } from './route-error-boundary';

function Boom(): never {
  throw new Error('boom');
}

describe('error boundaries', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('ErrorBoundary catches a render error and shows the branded fallback', () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    render(
      <ErrorBoundary>
        <Boom />
      </ErrorBoundary>,
    );
    expect(screen.getByText('Something went wrong')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /reload the app/i })).toBeInTheDocument();
  });

  it('RouteErrorBoundary renders for a thrown route error', () => {
    vi.spyOn(console, 'error').mockImplementation(() => {});
    renderWithRouter([{ path: '/', element: <Boom />, errorElement: <RouteErrorBoundary /> }]);
    expect(screen.getByText('Something went wrong')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /try again/i })).toBeInTheDocument();
  });
});
