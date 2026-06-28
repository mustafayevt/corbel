import { describe, expect, it } from 'vitest';
import { noteSchema } from './note-schemas';

describe('noteSchema', () => {
  it('accepts a titled note with content', () => {
    expect(noteSchema.safeParse({ title: 'Hi', content: 'body' }).success).toBe(true);
  });

  it('allows empty content', () => {
    expect(noteSchema.safeParse({ title: 'Hi', content: '' }).success).toBe(true);
  });

  it('requires a non-blank title', () => {
    expect(noteSchema.safeParse({ title: '   ', content: '' }).success).toBe(false);
  });

  it('caps title at 200 and content at 10,000 characters', () => {
    expect(noteSchema.safeParse({ title: 'a'.repeat(201), content: '' }).success).toBe(false);
    expect(noteSchema.safeParse({ title: 'ok', content: 'a'.repeat(10_001) }).success).toBe(false);
  });

  it('trims the title', () => {
    const result = noteSchema.safeParse({ title: '  Hi  ', content: '' });
    expect(result.success && result.data.title).toBe('Hi');
  });
});
