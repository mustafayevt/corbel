import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { describe, expect, it } from 'vitest';
import { makeNote, makeNotesPage } from '@/testing/mocks/data';
import { server } from '@/testing/mocks/server';
import { renderWithProviders } from '@/testing/render-with-providers';
import { NotesPage } from './notes-page';

const base = 'http://localhost';

describe('NotesPage', () => {
  it('renders the notes list', async () => {
    renderWithProviders(<NotesPage />);
    expect(await screen.findByText('First')).toBeInTheDocument();
    expect(screen.getByText('Second')).toBeInTheDocument();
  });

  it('shows the empty state when there are no notes', async () => {
    server.use(http.get(`${base}/api/notes`, () => HttpResponse.json(makeNotesPage([]))));
    renderWithProviders(<NotesPage />);
    expect(await screen.findByText(/no notes yet/i)).toBeInTheDocument();
  });

  it('drives the page query from the ?page= URL param', async () => {
    let requestedPage: string | null = null;
    server.use(
      http.get(`${base}/api/notes`, ({ request }) => {
        requestedPage = new URL(request.url).searchParams.get('page');
        return HttpResponse.json(
          makeNotesPage([makeNote({ title: 'Paged' })], {
            page: 2,
            totalPages: 3,
            totalCount: 25,
            hasNext: true,
            hasPrevious: true,
          }),
        );
      }),
    );
    renderWithProviders(<NotesPage />, { route: '/?page=2' });
    await screen.findByText('Paged');
    expect(requestedPage).toBe('2');
  });

  it('optimistically archives a note after confirmation', async () => {
    let archived = false;
    server.use(
      http.get(`${base}/api/notes`, () =>
        HttpResponse.json(
          makeNotesPage([makeNote({ id: 'n1', title: 'Solo', isArchived: archived })]),
        ),
      ),
      http.post(`${base}/api/notes/:id/archive`, () => {
        archived = true;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderWithProviders(<NotesPage />);

    await userEvent.click(await screen.findByRole('button', { name: /archive/i }));
    const dialog = await screen.findByRole('alertdialog');
    await userEvent.click(within(dialog).getByRole('button', { name: /archive/i }));

    expect(await screen.findByText('Archived')).toBeInTheDocument();
  });

  it('rolls back and toasts when archiving fails', async () => {
    server.use(
      http.get(`${base}/api/notes`, () =>
        HttpResponse.json(makeNotesPage([makeNote({ id: 'n1', title: 'Solo' })])),
      ),
      http.post(`${base}/api/notes/:id/archive`, () =>
        HttpResponse.json(
          { title: 'Server error', status: 500, errorCode: 'common.unexpected' },
          { status: 500 },
        ),
      ),
    );
    renderWithProviders(<NotesPage />);

    await userEvent.click(await screen.findByRole('button', { name: /archive/i }));
    const dialog = await screen.findByRole('alertdialog');
    await userEvent.click(within(dialog).getByRole('button', { name: /archive/i }));

    // Global MutationCache toast fires (Radix renders a visible + an SR-announce copy, hence findAll);
    // the optimistic badge rolls back so the Archive button returns.
    expect((await screen.findAllByText(/action failed/i)).length).toBeGreaterThan(0);
    await waitFor(() =>
      expect(screen.getByRole('button', { name: /archive/i })).toBeInTheDocument(),
    );
  });

  it('deletes a note after confirming the destructive dialog', async () => {
    let deleted = false;
    server.use(
      http.get(`${base}/api/notes`, () =>
        HttpResponse.json(makeNotesPage(deleted ? [] : [makeNote({ id: 'n1', title: 'Doomed' })])),
      ),
      http.delete(`${base}/api/notes/:id`, () => {
        deleted = true;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderWithProviders(<NotesPage />);

    await userEvent.click(await screen.findByRole('button', { name: /delete/i }));
    const dialog = await screen.findByRole('alertdialog');
    await userEvent.click(within(dialog).getByRole('button', { name: /delete/i }));

    expect(await screen.findByText(/no notes yet/i)).toBeInTheDocument();
  });
});
