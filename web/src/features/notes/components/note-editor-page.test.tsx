import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { HttpResponse, http } from 'msw';
import { describe, expect, it } from 'vitest';
import { makeNote } from '@/testing/mocks/data';
import { server } from '@/testing/mocks/server';
import { renderWithRouter } from '@/testing/render-with-providers';
import { NoteEditorPage } from './note-editor-page';

const base = 'http://localhost';
const routes = [
  { path: '/notes/new', element: <NoteEditorPage /> },
  { path: '/notes/:id', element: <NoteEditorPage /> },
  { path: '/', element: <div>Notes list</div> },
];

describe('NoteEditorPage', () => {
  it('creates a note and returns to the list', async () => {
    renderWithRouter(routes, { initialEntries: ['/notes/new'] });
    await userEvent.type(screen.getByLabelText(/title/i), 'Fresh note');
    await userEvent.click(screen.getByRole('button', { name: /create note/i }));
    await waitFor(() => expect(screen.getByText('Notes list')).toBeInTheDocument());
  });

  it('loads an existing note and saves changes', async () => {
    server.use(
      http.get(`${base}/api/notes/:id`, ({ params }) =>
        HttpResponse.json(makeNote({ id: String(params.id), title: 'Existing' })),
      ),
    );
    renderWithRouter(routes, { initialEntries: ['/notes/n1'] });
    expect(await screen.findByDisplayValue('Existing')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: /save changes/i }));
    await waitFor(() => expect(screen.getByText('Notes list')).toBeInTheDocument());
  });

  it('maps a 400 onto the title field', async () => {
    server.use(
      http.get(`${base}/api/notes/:id`, ({ params }) =>
        HttpResponse.json(makeNote({ id: String(params.id), title: 'Existing' })),
      ),
      http.put(`${base}/api/notes/:id`, () =>
        HttpResponse.json(
          {
            title: 'Validation',
            status: 400,
            errorCode: 'common.validation',
            errors: { Title: ['Title clash'] },
          },
          { status: 400 },
        ),
      ),
    );
    renderWithRouter(routes, { initialEntries: ['/notes/n1'] });
    await screen.findByDisplayValue('Existing');
    await userEvent.click(screen.getByRole('button', { name: /save changes/i }));
    expect(await screen.findByText('Title clash')).toBeInTheDocument();
  });

  it('shows a not-found message when the note is missing', async () => {
    server.use(
      // No title/detail → the editor shows its branded "doesn't exist" fallback.
      http.get(`${base}/api/notes/:id`, () =>
        HttpResponse.json({ status: 404, errorCode: 'note.not_found' }, { status: 404 }),
      ),
    );
    renderWithRouter(routes, { initialEntries: ['/notes/missing'] });
    expect(await screen.findByText(/doesn't exist/i)).toBeInTheDocument();
  });
});
