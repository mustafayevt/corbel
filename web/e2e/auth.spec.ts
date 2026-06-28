import AxeBuilder from '@axe-core/playwright';
import { expect, type Page, test } from '@playwright/test';

/** Run an axe WCAG 2.0/2.1 A + AA scan on the current page and assert zero violations. */
async function expectNoA11yViolations(page: Page) {
  const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa']).analyze();
  expect(results.violations).toEqual([]);
}

test.describe('Authentication', () => {
  test('login page renders and has no accessibility violations', async ({ page }) => {
    await page.goto('/login');

    await expect(page.getByRole('heading', { name: /sign in/i })).toBeVisible();
    await expect(page.getByLabel(/email/i)).toBeVisible();
    await expect(page.getByRole('button', { name: /sign in/i })).toBeVisible();

    await expectNoA11yViolations(page);
  });

  test('register page renders and has no accessibility violations', async ({ page }) => {
    await page.goto('/register');

    await expect(page.getByRole('heading', { name: /create your account/i })).toBeVisible();
    await expect(page.getByLabel(/email/i)).toBeVisible();
    await expect(page.getByLabel(/^password$/i)).toBeVisible();
    await expect(page.getByRole('button', { name: /create account/i })).toBeVisible();

    await expectNoA11yViolations(page);
  });

  // Happy path login → notes, fully mocked at the network layer with page.route so it runs in CI without
  // a backend. Each /api call the flow makes is fulfilled here.
  test('signs in and lands on the notes list', async ({ page }) => {
    // Boot silent-refresh: no session yet → 401 so the login form shows.
    await page.route('**/api/auth/refresh', (route) => route.fulfill({ status: 401 }));
    await page.route('**/api/auth/login', (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          accessToken: 'header.payload.signature',
          expiresIn: 900,
          refreshToken: null,
        }),
      }),
    );
    await page.route('**/api/auth/me', (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'u1',
          email: 'demo@corbel.dev',
          displayName: 'Demo',
          roles: ['User'],
        }),
      }),
    );
    await page.route('**/api/notes**', (route) =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            {
              id: '1',
              title: 'Welcome',
              content: 'Hi',
              isArchived: false,
              createdAtUtc: '2026-01-01T00:00:00Z',
            },
          ],
          page: 1,
          pageSize: 9,
          totalCount: 1,
          totalPages: 1,
          hasNext: false,
          hasPrevious: false,
        }),
      }),
    );

    await page.goto('/login');
    await page.getByLabel(/email/i).fill('demo@corbel.dev');
    await page.getByLabel(/^password$/i).fill('Password123!');
    await page.getByRole('button', { name: /sign in/i }).click();

    await expect(page.getByRole('heading', { name: /your notes/i })).toBeVisible();
    await expect(page.getByText('Welcome')).toBeVisible();
  });
});
