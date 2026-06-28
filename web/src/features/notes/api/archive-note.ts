import { useQueryClient } from '@tanstack/react-query';
import { $api } from '@/lib/react-query';
import type { NotesPage } from '@/types/api';
import { notesKeys } from './notes-keys';

/**
 * Optimistic archive: flip `isArchived` across every cached list page immediately, roll back on error, and
 * reconcile with the server on settle. Each NoteCard owns its own instance so `isPending` is per-card.
 */
export function useArchiveNote() {
  const qc = useQueryClient();
  return $api.useMutation('post', '/api/notes/{id}/archive', {
    onMutate: async (variables) => {
      const { id } = variables.params.path;
      await qc.cancelQueries({ queryKey: notesKeys.list() });
      const previous = qc.getQueriesData<NotesPage>({ queryKey: notesKeys.list() });
      qc.setQueriesData<NotesPage>({ queryKey: notesKeys.list() }, (page) =>
        page
          ? {
              ...page,
              items: page.items.map((note) =>
                note.id === id ? { ...note, isArchived: true } : note,
              ),
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
