import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { createMemoryRouter, RouterProvider } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { useAuthStore } from '@/features/auth/stores/auth-store';
import { server } from '@/testing/mocks/server';
import { ErrorCode } from '@/types/api';
import { LoginPage } from './login-page';

function renderLogin() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const router = createMemoryRouter(
    [
      { path: '/login', element: <LoginPage /> },
      // Stand-in for the protected notes route so we can assert the post-login redirect cheaply.
      { path: '/', element: <div>Notes dashboard</div> },
    ],
    { initialEntries: ['/login'] },
  );
  return render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

describe('LoginPage', () => {
  it('stores the access token and redirects on success', async () => {
    server.use(
      http.post('http://localhost/api/auth/login', () =>
        HttpResponse.json({ accessToken: 'header.payload.signature', expiresIn: 900 }),
      ),
    );

    renderLogin();

    await userEvent.type(screen.getByLabelText(/email/i), 'ada@example.com');
    await userEvent.type(screen.getByLabelText(/^password$/i), 'correct-horse');
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }));

    await waitFor(() => expect(screen.getByText('Notes dashboard')).toBeInTheDocument());
    expect(useAuthStore.getState().accessToken).toBe('header.payload.signature');
  });

  it('shows the server error and keeps the user signed out on bad credentials', async () => {
    server.use(
      http.post('http://localhost/api/auth/login', () =>
        HttpResponse.json(
          {
            title: 'Unauthorized',
            status: 401,
            errorCode: 'auth.invalid_credentials',
            detail: 'Invalid email or password.',
          },
          { status: 401 },
        ),
      ),
    );

    renderLogin();

    await userEvent.type(screen.getByLabelText(/email/i), 'ada@example.com');
    await userEvent.type(screen.getByLabelText(/^password$/i), 'wrong-password');
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }));

    expect(await screen.findByText(/invalid email or password/i)).toBeInTheDocument();
    expect(useAuthStore.getState().accessToken).toBeNull();
  });

  it('shows a tailored message when the account is locked', async () => {
    server.use(
      http.post('http://localhost/api/auth/login', () =>
        HttpResponse.json(
          {
            title: 'Unauthorized',
            status: 401,
            errorCode: ErrorCode.AccountLocked,
            detail: 'The account is temporarily locked. Try again later.',
          },
          { status: 401 },
        ),
      ),
    );

    renderLogin();

    await userEvent.type(screen.getByLabelText(/email/i), 'ada@example.com');
    await userEvent.type(screen.getByLabelText(/^password$/i), 'correct-horse');
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }));

    expect(await screen.findByText(/temporarily locked/i)).toBeInTheDocument();
    expect(useAuthStore.getState().accessToken).toBeNull();
  });

  it('validates the form client-side before calling the API', async () => {
    renderLogin();

    // No MSW handler registered: if the form submitted, the unhandled request would fail the test.
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }));

    expect(await screen.findByText(/enter a valid email address/i)).toBeInTheDocument();
    expect(screen.getByText(/password is required/i)).toBeInTheDocument();
    expect(useAuthStore.getState().accessToken).toBeNull();
  });
});
