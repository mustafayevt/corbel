import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { useAuthStore } from '@/features/auth/stores/auth-store';
import { renderWithRouter } from '@/testing/render-with-providers';
import { ProtectedRoute } from './protected-route';

const routes = [
  {
    path: '/',
    element: <ProtectedRoute />,
    children: [{ index: true, element: <div>Protected content</div> }],
  },
  { path: '/login', element: <div>Login screen</div> },
];

describe('ProtectedRoute', () => {
  it('holds a splash while the on-boot refresh is still resolving', () => {
    useAuthStore.setState({ accessToken: null, hasBootstrapped: false });
    renderWithRouter(routes);
    expect(screen.getByText(/loading your session/i)).toBeInTheDocument();
  });

  it('redirects an unauthenticated, bootstrapped user to /login', () => {
    useAuthStore.setState({ accessToken: null, hasBootstrapped: true });
    renderWithRouter(routes);
    expect(screen.getByText('Login screen')).toBeInTheDocument();
  });

  it('renders the protected outlet for an authenticated user', () => {
    useAuthStore.setState({ accessToken: 'token', hasBootstrapped: true });
    renderWithRouter(routes);
    expect(screen.getByText('Protected content')).toBeInTheDocument();
  });
});
