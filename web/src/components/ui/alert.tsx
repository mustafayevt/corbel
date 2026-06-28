import { cva, type VariantProps } from 'class-variance-authority';
import type { ComponentProps } from 'react';
import { cn } from '@/lib/utils';

const alertVariants = cva('rounded-md border px-4 py-3 text-sm', {
  variants: {
    variant: {
      destructive: 'border-destructive/40 bg-destructive/10 text-destructive',
      success: 'border-emerald-600/40 bg-emerald-600/10 text-emerald-700 dark:text-emerald-400',
      info: 'border-border bg-muted text-foreground',
    },
  },
  defaultVariants: {
    variant: 'info',
  },
});

export type AlertProps = ComponentProps<'div'> & VariantProps<typeof alertVariants>;

export function Alert({ className, variant, role, ...props }: AlertProps) {
  // Errors interrupt (assertive role="alert"); success/info are non-urgent (polite role="status"). An explicit
  // `role` prop still wins. This keeps a registration-success notice from rudely cutting off the screen reader.
  const resolvedRole = role ?? (variant === 'destructive' ? 'alert' : 'status');
  return (
    <div role={resolvedRole} className={cn(alertVariants({ variant }), className)} {...props} />
  );
}
