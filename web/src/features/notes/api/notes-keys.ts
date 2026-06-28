import { $api } from '@/lib/react-query';

/**
 * Typed query keys for the notes feature, derived from openapi-react-query's `queryOptions`. Building keys this
 * way (instead of hand-writing `['get', '/api/notes']`) means a method/path typo is a compile error rather than a
 * silently-missed cache invalidation. `list()` yields the `['get', '/api/notes']` prefix that matches every
 * paginated page; `detail(id)` targets a single note's cache entry.
 */
export const notesKeys = {
  list: () => $api.queryOptions('get', '/api/notes').queryKey,
  detail: (id: string) =>
    $api.queryOptions('get', '/api/notes/{id}', { params: { path: { id } } }).queryKey,
} as const;
