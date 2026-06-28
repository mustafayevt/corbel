import type { FieldValues, Path, UseFormSetError } from 'react-hook-form';
import { isProblemDetails, type ProblemDetails } from '@/types/api';

/** Narrow an unknown thrown value to ProblemDetails, or null. openapi-fetch rejects mutations/queries
 *  with the parsed error body, so this is what `onError` receives. */
export function asProblem(error: unknown): ProblemDetails | null {
  return isProblemDetails(error) ? error : null;
}

/** A human-readable message for a thrown error: the server's ProblemDetails detail/title, else the caller's
 *  fallback. Native errors are deliberately NOT surfaced — openapi-fetch throws a browser-specific TypeError on a
 *  network failure ("Failed to fetch" / "Load failed"), so the authored call-site fallback reads far better. */
export function getErrorMessage(
  error: unknown,
  fallback = 'Something went wrong. Please try again.',
): string {
  const problem = asProblem(error);
  if (problem) {
    return problem.detail || problem.title || fallback;
  }
  return fallback;
}

/**
 * Map a 400 ProblemDetails `errors` dictionary onto react-hook-form field errors. Backend keys are
 * matched case-insensitively against the form's known fields (PascalCase ↔ camelCase). Returns true if
 * any field error was applied, so the caller can decide whether to also surface a form-level message.
 */
export function applyProblemDetails<TFieldValues extends FieldValues>(
  error: unknown,
  setError: UseFormSetError<TFieldValues>,
  knownFields: readonly Path<TFieldValues>[],
): boolean {
  const problem = asProblem(error);
  if (!problem?.errors) {
    return false;
  }
  let applied = false;
  for (const [rawKey, messages] of Object.entries(problem.errors)) {
    const field = knownFields.find((candidate) => candidate.toLowerCase() === rawKey.toLowerCase());
    const message = messages?.[0];
    if (field && message) {
      // Move focus to the first invalid field so keyboard/screen-reader users land on what to fix.
      setError(field, { type: 'server', message }, { shouldFocus: !applied });
      applied = true;
    }
  }
  return applied;
}
