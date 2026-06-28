import { HttpResponse, http } from 'msw';
import { makeNote, makeNotesPage, makeTokenResponse, makeUser } from './data';

// Tests run with VITE_API_BASE_URL=http://localhost (see vite.config test env), so handlers are absolute.
const base = 'http://localhost';

/** Default happy-path handlers registered for every test; override per-case with `server.use(...)`. */
export const handlers = [
  http.post(`${base}/api/auth/login`, () => HttpResponse.json(makeTokenResponse())),
  http.post(`${base}/api/auth/register`, () => HttpResponse.json({ message: 'ok' })),
  // Boot refresh defaults to "no session" so component tests don't accidentally authenticate.
  http.post(`${base}/api/auth/refresh`, () => new HttpResponse(null, { status: 401 })),
  http.post(`${base}/api/auth/logout`, () => HttpResponse.json({ message: 'ok' })),
  http.get(`${base}/api/auth/me`, () => HttpResponse.json(makeUser())),
  http.get(`${base}/api/notes`, () =>
    HttpResponse.json(makeNotesPage([makeNote({ title: 'First' }), makeNote({ title: 'Second' })])),
  ),
  http.get(`${base}/api/notes/:id`, ({ params }) =>
    HttpResponse.json(makeNote({ id: String(params.id) })),
  ),
  http.post(`${base}/api/notes`, () => HttpResponse.json(makeNote(), { status: 201 })),
  http.put(`${base}/api/notes/:id`, ({ params }) =>
    HttpResponse.json(makeNote({ id: String(params.id) })),
  ),
  http.post(`${base}/api/notes/:id/archive`, ({ params }) =>
    HttpResponse.json(makeNote({ id: String(params.id), isArchived: true })),
  ),
  http.delete(`${base}/api/notes/:id`, () => new HttpResponse(null, { status: 204 })),
];
