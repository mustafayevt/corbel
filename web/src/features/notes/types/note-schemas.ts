import { z } from 'zod';

export const noteSchema = z.object({
  title: z
    .string()
    .trim()
    .min(1, 'Title is required')
    .max(200, 'Keep the title under 200 characters'),
  // Empty content is allowed (the domain treats null/blank as ""); the backend caps content at 10,000.
  content: z.string().max(10_000, 'Keep the note under 10,000 characters'),
});

export type NoteFormValues = z.infer<typeof noteSchema>;
