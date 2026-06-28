import { z } from 'zod';

// Zod 4 top-level format validators (`z.email`) replace the deprecated `z.string().email()`.
const email = z.email('Enter a valid email address');

// Mirror the backend password policy (Common/Validation/PasswordRules.cs is the single source of truth):
// 8–128 characters with a lowercase, an uppercase and a digit. ASP.NET Identity's own char-class rules are
// disabled, so all complexity lives in PasswordRules. (The ASCII classes here are intentionally a touch
// stricter than the server's Unicode-aware char.Is* checks — the client only ever rejects more, never less.)
const registerPassword = z
  .string()
  .min(8, 'Use at least 8 characters')
  .max(128, 'Keep it under 128 characters')
  .regex(/[a-z]/, 'Add a lowercase letter')
  .regex(/[A-Z]/, 'Add an uppercase letter')
  .regex(/[0-9]/, 'Add a number');

export const loginSchema = z.object({
  email,
  password: z.string().min(1, 'Password is required'),
});
export type LoginValues = z.infer<typeof loginSchema>;

export const registerSchema = z
  .object({
    email,
    displayName: z.string().trim().max(100, 'Keep it under 100 characters').optional(),
    password: registerPassword,
    confirmPassword: z.string(),
  })
  .refine((values) => values.password === values.confirmPassword, {
    message: 'Passwords do not match',
    path: ['confirmPassword'],
  });
export type RegisterValues = z.infer<typeof registerSchema>;
