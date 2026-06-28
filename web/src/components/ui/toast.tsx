import * as ToastPrimitive from '@radix-ui/react-toast';
import { cva, type VariantProps } from 'class-variance-authority';
import type { ComponentProps } from 'react';
import { cn } from '@/lib/utils';

/** Wraps the app so store-driven toasts can render anywhere (mounted in app/provider). */
export const ToastProvider = ToastPrimitive.Provider;

export function ToastViewport({
  className,
  ...props
}: ComponentProps<typeof ToastPrimitive.Viewport>) {
  return (
    <ToastPrimitive.Viewport
      className={cn(
        'fixed bottom-0 right-0 z-[100] flex max-h-screen w-full flex-col gap-2 p-4 outline-none sm:max-w-sm',
        className,
      )}
      {...props}
    />
  );
}

const toastVariants = cva(
  'pointer-events-auto relative flex w-full items-start gap-3 rounded-md border px-4 py-3 text-sm shadow-lg data-[swipe=move]:translate-x-[var(--radix-toast-swipe-move-x)] data-[swipe=cancel]:translate-x-0 data-[state=open]:animate-in data-[state=closed]:animate-out',
  {
    variants: {
      variant: {
        info: 'border-border bg-card text-card-foreground',
        success: 'border-emerald-600/40 bg-emerald-600/10 text-emerald-700 dark:text-emerald-400',
        warning: 'border-amber-600/40 bg-amber-600/10 text-amber-700 dark:text-amber-400',
        error: 'border-destructive/40 bg-destructive/10 text-destructive',
      },
    },
    defaultVariants: { variant: 'info' },
  },
);

export type ToastProps = ComponentProps<typeof ToastPrimitive.Root> &
  VariantProps<typeof toastVariants>;

export function Toast({ className, variant, ...props }: ToastProps) {
  return <ToastPrimitive.Root className={cn(toastVariants({ variant }), className)} {...props} />;
}

export function ToastTitle({ className, ...props }: ComponentProps<typeof ToastPrimitive.Title>) {
  return <ToastPrimitive.Title className={cn('font-medium', className)} {...props} />;
}

export function ToastDescription({
  className,
  ...props
}: ComponentProps<typeof ToastPrimitive.Description>) {
  return (
    <ToastPrimitive.Description className={cn('text-muted-foreground', className)} {...props} />
  );
}

export const ToastClose = ToastPrimitive.Close;
