import { useQueryClient } from '@tanstack/react-query';
import { $api } from '@/lib/react-query';
import type { NotesPage } from '@/types/api';
import { notesKeys } from './notes-keys';

/**
 * Optimistic delete: drop the note from every cached list page and decrement the count immediately, roll
 * back on error, reconcile on settle. The notes page's clamp effect handles stepping back an emptied page.
 */
export function useDeleteNote() {
  const qc = useQueryClient();
  return $api.useMutation('delete', '/api/notes/{id}', {
    onMutate: async (variables) => {
      const { id } = variables.params.path;
      await qc.cancelQueries({ queryKey: notesKeys.list() });
      const previous = qc.getQueriesData<NotesPage>({ queryKey: notesKeys.list() });
      qc.setQueriesData<NotesPage>({ queryKey: notesKeys.list() }, (page) =>
        page
          ? {
              ...page,
              items: page.items.filter((note) => note.id !== id),
              totalCount: Math.max(page.totalCount - 1, 0),
            }
          : page,
      );
      return { previous };
    },
    onError: (_error, _variables, context) => {
      context?.previous?.forEach(([key, data]) => {
        qc.setQueryData(key, data);
      });
    },
    onSettled: () => qc.invalidateQueries({ queryKey: notesKeys.list() }),
  });
}
