import type { components } from './schema';

/** Convenience aliases over the generated component schemas, so features import friendly names. */
export type Note = components['schemas']['NoteResponse'];
export type NotesPage = components['schemas']['PagedResultOfNoteResponse'];

/**
 * RFC 9457 ProblemDetails as returned by the API's GlobalExceptionHandler — derived from the generated schema so
 * it can't drift from the real contract. `errorCode` and `traceId` are added to every error body by the handler;
 * `errors` is populated only on a 400 validation failure (it lives on HttpValidationProblemDetails in the spec).
 */
export type ProblemDetails = components['schemas']['ProblemDetails'] &
  Partial<Pick<components['schemas']['HttpValidationProblemDetails'], 'errors'>>;

/**
 * Friendly named constants for the stable `errorCode` strings, so UI code can branch on
 * `ErrorCode.AccountLocked` instead of a magic string. Hand-maintained to mirror the backend
 * `Corbel.Common.Errors.ErrorCodes`; keep the two in sync when you add a code.
 */
export const ErrorCode = {
  Validation: 'common.validation',
  Forbidden: 'common.forbidden',
  Unauthorized: 'common.unauthorized',
  NotFound: 'common.not_found',
  RateLimited: 'common.rate_limited',
  Concurrency: 'common.concurrency_conflict',
  Unexpected: 'common.unexpected',
  InvalidCredentials: 'auth.invalid_credentials',
  AccountLocked: 'auth.account_locked',
  InvalidToken: 'auth.invalid_token',
  TokenReuseDetected: 'auth.token_reuse_detected',
  NoteNotFound: 'note.not_found',
  NoteAlreadyArchived: 'note.already_archived',
} as const;

/** Narrows an unknown thrown value (openapi-fetch surfaces the parsed JSON body) to ProblemDetails. */
export function isProblemDetails(value: unknown): value is ProblemDetails {
  // Key on RFC 9457 signals, not `title` (domain payloads like NoteResponse also carry a `title`): this backend
  // always sets a stable `errorCode`, and any ProblemDetails carries a numeric `status`.
  return (
    typeof value === 'object' &&
    value !== null &&
    ('errorCode' in value || typeof (value as { status?: unknown }).status === 'number')
  );
}
