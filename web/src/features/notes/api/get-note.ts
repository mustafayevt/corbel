import { type QueryClient, useQueryClient } from '@tanstack/react-query';
import { $api } from '@/lib/react-query';
import type { Note, NotesPage } from '@/types/api';
import { notesKeys } from './notes-keys';

/** Find a note already present in any cached list page, to seed the editor instantly. */
function findNoteInListCache(client: QueryClient, id: string): Note | undefined {
  const pages = client.getQueriesData<NotesPage>({ queryKey: notesKeys.list() });
  for (const [, page] of pages) {
    const hit = page?.items.find((note) => note.id === id);
    if (hit) {
      return hit;
    }
  }
  return undefined;
}

export function useNote(id: string | undefined, options?: { enabled?: boolean }) {
  const client = useQueryClient();
  return $api.useQuery(
    'get',
    '/api/notes/{id}',
    { params: { path: { id: id ?? '' } } },
    {
      enabled: options?.enabled ?? Boolean(id),
      // Render the list-cached note immediately (then revalidate in the background) so edit feels instant.
      placeholderData: () => (id ? findNoteInListCache(client, id) : undefined),
    },
  );
}

/** Imperative prefetch used on NoteCard hover/focus so the editor is already warm on click. */
export function usePrefetchNote() {
  const qc = useQueryClient();
  return (id: string) =>
    qc.prefetchQuery(
      $api.queryOptions(
        'get',
        '/api/notes/{id}',
        { params: { path: { id } } },
        { staleTime: 30_000 },
      ),
    );
}
