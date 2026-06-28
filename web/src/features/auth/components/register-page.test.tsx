import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { describe, expect, it } from 'vitest';
import { useAuthStore } from '@/features/auth/stores/auth-store';
import { server } from '@/testing/mocks/server';
import { renderWithRouter } from '@/testing/render-with-providers';
import { RegisterPage } from './register-page';

const routes = [
  { path: '/register', element: <RegisterPage /> },
  { path: '/login', element: <div>Login screen</div> },
  { path: '/', element: <div>Home</div> },
];

async function fillValidForm() {
  await userEvent.type(screen.getByLabelText(/display name/i), 'Ada');
  await userEvent.type(screen.getByLabelText(/email/i), 'ada@example.com');
  await userEvent.type(screen.getByLabelText(/^password$/i), 'Password1');
  await userEvent.type(screen.getByLabelText(/confirm password/i), 'Password1');
}

describe('RegisterPage', () => {
  it('creates the account and redirects to sign-in', async () => {
    renderWithRouter(routes, { initialEntries: ['/register'] });
    await fillValidForm();
    await userEvent.click(screen.getByRole('button', { name: /create account/i }));
    await waitFor(() => expect(screen.getByText('Login screen')).toBeInTheDocument());
  });

  it('maps a 400 ProblemDetails onto the email field', async () => {
    server.use(
      http.post('http://localhost/api/auth/register', () =>
        HttpResponse.json(
          {
            title: 'Validation failed',
            status: 400,
            errorCode: 'common.validation',
            errors: { Email: ['That email is already registered.'] },
          },
          { status: 400 },
        ),
      ),
    );
    renderWithRouter(routes, { initialEntries: ['/register'] });
    await fillValidForm();
    await userEvent.click(screen.getByRole('button', { name: /create account/i }));
    expect(await screen.findByText(/already registered/i)).toBeInTheDocument();
  });

  it('redirects an already-authenticated user away from the form', () => {
    useAuthStore.setState({ accessToken: 'token', hasBootstrapped: true });
    renderWithRouter(routes, { initialEntries: ['/register'] });
    expect(screen.getByText('Home')).toBeInTheDocument();
  });
});
