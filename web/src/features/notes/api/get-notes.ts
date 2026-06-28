import { keepPreviousData } from '@tanstack/react-query';
import { $api } from '@/lib/react-query';

interface UseNotesParams {
  page: number;
  pageSize: number;
}

/** Paginated notes list. keepPreviousData holds the current page visible while the next one loads. */
export function useNotes({ page, pageSize }: UseNotesParams) {
  return $api.useQuery(
    'get',
    '/api/notes',
    { params: { query: { page, pageSize } } },
    { placeholderData: keepPreviousData },
  );
}
