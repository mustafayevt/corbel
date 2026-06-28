import { describe, expect, it } from 'vitest';
import { loginSchema, registerSchema } from './auth-schemas';

describe('loginSchema', () => {
  it('accepts a valid email + non-empty password', () => {
    expect(loginSchema.safeParse({ email: 'ada@example.com', password: 'x' }).success).toBe(true);
  });

  it('rejects a malformed email and an empty password', () => {
    const result = loginSchema.safeParse({ email: 'nope', password: '' });
    expect(result.success).toBe(false);
  });
});

describe('registerSchema', () => {
  const valid = {
    email: 'ada@example.com',
    displayName: 'Ada',
    password: 'Password1',
    confirmPassword: 'Password1',
  };

  it('accepts a valid registration', () => {
    expect(registerSchema.safeParse(valid).success).toBe(true);
  });

  it('enforces the password policy (upper, lower, digit, length)', () => {
    expect(
      registerSchema.safeParse({ ...valid, password: 'short', confirmPassword: 'short' }).success,
    ).toBe(false);
    expect(
      registerSchema.safeParse({
        ...valid,
        password: 'alllowercase1',
        confirmPassword: 'alllowercase1',
      }).success,
    ).toBe(false);
    expect(
      registerSchema.safeParse({
        ...valid,
        password: 'NoDigitsHere',
        confirmPassword: 'NoDigitsHere',
      }).success,
    ).toBe(false);
  });

  it('requires the confirmation to match', () => {
    const result = registerSchema.safeParse({ ...valid, confirmPassword: 'Different1' });
    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues.some((issue) => issue.path.includes('confirmPassword'))).toBe(
        true,
      );
    }
  });

  it('treats displayName as optional', () => {
    const { displayName, ...withoutName } = valid;
    void displayName;
    expect(registerSchema.safeParse(withoutName).success).toBe(true);
  });
});
