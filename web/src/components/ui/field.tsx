import { cloneElement, type ReactElement } from 'react';
import { Label } from './label';

/** Props Field injects onto the control it wraps. */
interface ControlProps {
  id?: string;
  'aria-invalid'?: boolean;
  'aria-describedby'?: string;
}

interface FieldProps {
  id: string;
  label: string;
  error?: string;
  hint?: string;
  /** The single form control. Field injects `id`, `aria-invalid`, and `aria-describedby` (hint + error). */
  children: ReactElement<ControlProps>;
}

/**
 * Label + control + (optional) hint/error, with the accessibility wiring owned here so call sites just render
 * the control: Field associates the label (htmlFor/id), points the control's `aria-describedby` at whichever of
 * the hint/error it renders, sets `aria-invalid` when there's an error, and marks the error as a live region so
 * a screen reader announces it (including server-applied errors that don't move focus).
 */
export function Field({ id, label, error, hint, children }: FieldProps) {
  const hintId = hint ? `${id}-hint` : undefined;
  const errorId = error ? `${id}-error` : undefined;
  const describedBy =
    [hintId, errorId, children.props['aria-describedby']].filter(Boolean).join(' ') || undefined;

  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>{label}</Label>
      {cloneElement(children, {
        id,
        'aria-invalid': error ? true : children.props['aria-invalid'],
        'aria-describedby': describedBy,
      })}
      {hint ? (
        <p id={hintId} className="text-xs text-muted-foreground">
          {hint}
        </p>
      ) : null}
      {error ? (
        <p id={errorId} role="alert" className="text-xs font-medium text-destructive">
          {error}
        </p>
      ) : null}
    </div>
  );
}
