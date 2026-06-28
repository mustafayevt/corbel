import type { Note, NotesPage } from '@/types/api';

let noteCounter = 0;

export function makeNote(overrides: Partial<Note> = {}): Note {
  noteCounter += 1;
  return {
    id: `note-${noteCounter}`,
    title: 'Test note',
    content: 'Body',
    isArchived: false,
    createdAtUtc: '2026-01-01T00:00:00Z',
    ...overrides,
  };
}

export function makeNotesPage(items: Note[], overrides: Partial<NotesPage> = {}): NotesPage {
  return {
    items,
    page: 1,
    pageSize: 9,
    totalCount: items.length,
    totalPages: Math.max(Math.ceil(items.length / 9), 1),
    hasNext: false,
    hasPrevious: false,
    ...overrides,
  };
}

/** JWT-shaped token whose middle segment decodes to real claims (mirrors decodeUser's expectations). */
export function makeToken(sub = 'user-1'): string {
  const payload = btoa(JSON.stringify({ sub, email: 'ada@example.com', name: 'Ada' }))
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '');
  return `header.${payload}.signature`;
}

export const makeTokenResponse = () => ({
  accessToken: makeToken(),
  expiresIn: 900,
  refreshToken: null,
});

export const makeUser = (roles: string[] = ['User']) => ({
  id: 'user-1',
  email: 'ada@example.com',
  displayName: 'Ada',
  roles,
});
