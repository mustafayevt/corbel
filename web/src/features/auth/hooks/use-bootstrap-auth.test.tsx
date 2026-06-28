import { render, screen, waitFor } from '@testing-library/react';
import { HttpResponse, http } from 'msw';
import { describe, expect, it } from 'vitest';
import { useAuthStore } from '@/features/auth/stores/auth-store';
import { makeTokenResponse, makeUser } from '@/testing/mocks/data';
import { server } from '@/testing/mocks/server';
import { useBootstrapAuth } from './use-bootstrap-auth';

/** Probe component that runs the boot hook and surfaces the resulting auth state. */
function BootProbe() {
  useBootstrapAuth();
  const accessToken = useAuthStore((s) => s.accessToken);
  const email = useAuthStore((s) => s.user?.email);
  const hasBootstrapped = useAuthStore((s) => s.hasBootstrapped);
  return (
    <div>
      <span data-testid="bootstrapped">{String(hasBootstrapped)}</span>
      <span data-testid="token">{accessToken ?? 'none'}</span>
      <span data-testid="email">{email ?? 'none'}</span>
    </div>
  );
}

describe('useBootstrapAuth', () => {
  it('marks bootstrapped and stays signed out when the silent refresh 401s', async () => {
    // The default handler 401s /api/auth/refresh (the "no cookie session" case).
    render(<BootProbe />);

    await waitFor(() => expect(screen.getByTestId('bootstrapped')).toHaveTextContent('true'));
    expect(screen.getByTestId('token')).toHaveTextContent('none');
  });

  it('restores the session and hydrates identity when refresh + /me succeed', async () => {
    const tokens = makeTokenResponse();
    server.use(
      http.post('http://localhost/api/auth/refresh', () => HttpResponse.json(tokens)),
      http.get('http://localhost/api/auth/me', () => HttpResponse.json(makeUser())),
    );

    render(<BootProbe />);

    await waitFor(() => expect(screen.getByTestId('token')).toHaveTextContent(tokens.accessToken));
    expect(screen.getByTestId('email')).toHaveTextContent('ada@example.com');
    expect(screen.getByTestId('bootstrapped')).toHaveTextContent('true');
  });

  it('still finishes bootstrapping with the token when refresh succeeds but /me fails', async () => {
    const tokens = makeTokenResponse();
    server.use(
      http.post('http://localhost/api/auth/refresh', () => HttpResponse.json(tokens)),
      http.get('http://localhost/api/auth/me', () => new HttpResponse(null, { status: 500 })),
    );

    render(<BootProbe />);

    await waitFor(() => expect(screen.getByTestId('bootstrapped')).toHaveTextContent('true'));
    expect(screen.getByTestId('token')).toHaveTextContent(tokens.accessToken);
  });
});
