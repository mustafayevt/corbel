import type { ComponentProps } from 'react';
import { cn } from '@/lib/utils';

// React 19: `ref` is a normal prop, so spreading react-hook-form's `register(...)` (which includes ref)
// onto the element wires up validation without `forwardRef`.
export function Input({ className, type, ...props }: ComponentProps<'input'>) {
  return (
    <input
      type={type}
      className={cn(
        'flex h-10 w-full rounded-md border border-input bg-transparent px-3 py-2 text-sm shadow-sm transition-colors',
        'placeholder:text-muted-foreground',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background',
        'aria-[invalid=true]:border-destructive aria-[invalid=true]:ring-destructive/30',
        'disabled:cursor-not-allowed disabled:opacity-50',
        className,
      )}
      {...props}
    />
  );
}
