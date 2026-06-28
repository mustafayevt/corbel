/**
 * Route patterns (consumed by the router) plus `getHref` builders for the links that take parameters, so a
 * path change stays a single edit. Static links (`/`, `/register`) use the literal directly.
 */
export const paths = {
  login: {
    path: '/login',
    getHref: (returnTo?: string) =>
      `/login${returnTo ? `?returnTo=${encodeURIComponent(returnTo)}` : ''}`,
  },
  register: { path: '/register' },
  notes: {
    root: { path: '/' },
    new: { path: 'notes/new', getHref: () => '/notes/new' },
    detail: { path: 'notes/:id', getHref: (id: string) => `/notes/${id}` },
  },
} as const;
