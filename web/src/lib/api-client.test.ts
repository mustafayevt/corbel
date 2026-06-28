import { HttpResponse, http } from 'msw';
import { beforeEach, describe, expect, it } from 'vitest';
import { useAuthStore } from '@/features/auth/stores/auth-store';
import { makeNotesPage, makeToken } from '@/testing/mocks/data';
import { server } from '@/testing/mocks/server';
import { client } from './api-client';

const STALE = makeToken('stale');
const FRESH = makeToken('fresh');

/** A /api/notes handler that 401s on the stale token and 200s once the refreshed token is presented. */
function notesGuardedByFreshToken(onCall?: () => void) {
  return http.get('http://localhost/api/notes', ({ request }) => {
    onCall?.();
    return request.headers.get('Authorization') === `Bearer ${FRESH}`
      ? HttpResponse.json(makeNotesPage([]))
      : new HttpResponse(null, { status: 401 });
  });
}

describe('auth middleware: 401 → single-flight refresh → retry', () => {
  beforeEach(() => {
    // Start authenticated with a token the API will reject, forcing the refresh path.
    useAuthStore.setState({ accessToken: STALE, user: null, hasBootstrapped: true });
  });

  it('refreshes once and replays the original request with the new token', async () => {
    let refreshCalls = 0;
    let notesCalls = 0;
    server.use(
      http.post('http://localhost/api/auth/refresh', () => {
        refreshCalls += 1;
        return HttpResponse.json({ accessToken: FRESH, expiresIn: 900 });
      }),
      notesGuardedByFreshToken(() => {
        notesCalls += 1;
      }),
    );

    const { data, error } = await client.GET('/api/notes', {});

    expect(error).toBeUndefined();
    expect(data).toBeDefined();
    expect(refreshCalls).toBe(1);
    expect(notesCalls).toBe(2); // original 401 + replayed 200
    expect(useAuthStore.getState().accessToken).toBe(FRESH);
  });

  it('collapses concurrent 401s onto a single refresh', async () => {
    let refreshCalls = 0;
    server.use(
      http.post('http://localhost/api/auth/refresh', () => {
        refreshCalls += 1;
        return HttpResponse.json({ accessToken: FRESH, expiresIn: 900 });
      }),
      notesGuardedByFreshToken(),
    );

    const results = await Promise.all([
      client.GET('/api/notes', {}),
      client.GET('/api/notes', {}),
      client.GET('/api/notes', {}),
    ]);

    for (const { error } of results) {
      expect(error).toBeUndefined();
    }
    expect(refreshCalls).toBe(1); // single-flight: all three waiters share one refresh
  });

  it('surfaces the original 401 and hard-logs-out when the refresh itself fails (no retry loop)', async () => {
    let refreshCalls = 0;
    let notesCalls = 0;
    server.use(
      http.post('http://localhost/api/auth/refresh', () => {
        refreshCalls += 1;
        return new HttpResponse(null, { status: 401 });
      }),
      http.get('http://localhost/api/notes', () => {
        notesCalls += 1;
        return new HttpResponse(null, { status: 401 });
      }),
    );

    const { error } = await client.GET('/api/notes', {});

    expect(error).toBeDefined(); // the original 401 is surfaced
    expect(refreshCalls).toBe(1); // refresh attempted exactly once
    expect(notesCalls).toBe(1); // and NOT retried after the failed refresh
    expect(useAuthStore.getState().accessToken).toBeNull(); // hard logout cleared the session
  });

  it('does not attempt a refresh when an auth route itself returns 401', async () => {
    let refreshCalls = 0;
    server.use(
      http.post('http://localhost/api/auth/refresh', () => {
        refreshCalls += 1;
        return HttpResponse.json({ accessToken: FRESH, expiresIn: 900 });
      }),
      http.post('http://localhost/api/auth/login', () => new HttpResponse(null, { status: 401 })),
    );

    const { error } = await client.POST('/api/auth/login', {
      body: { email: 'ada@example.com', password: 'nope', useCookies: false },
    });

    expect(error).toBeDefined();
    expect(refreshCalls).toBe(0); // a 401 from /api/auth/* is an auth outcome, never a token-expiry refresh
  });
});
