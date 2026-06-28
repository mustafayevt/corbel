import type { ComponentProps } from 'react';
import { cn } from '@/lib/utils';

export function Label({ className, ...props }: ComponentProps<'label'>) {
  return (
    // biome-ignore lint/a11y/noLabelWithoutControl: reusable primitive; association is the caller's via htmlFor
    <label className={cn('text-sm font-medium leading-none', className)} {...props} />
  );
}
