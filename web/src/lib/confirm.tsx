import { create } from 'zustand';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { buttonVariants } from '@/components/ui/button';
import { cn } from '@/lib/utils';

export interface ConfirmOptions {
  title: string;
  description?: string;
  confirmLabel?: string;
  cancelLabel?: string;
  destructive?: boolean;
}

interface ConfirmState {
  request: (ConfirmOptions & { resolve: (ok: boolean) => void }) | null;
  confirm: (options: ConfirmOptions) => Promise<boolean>;
  resolve: (ok: boolean) => void;
}

const useConfirmStore = create<ConfirmState>((set, get) => ({
  request: null,
  confirm: (options) =>
    new Promise<boolean>((resolve) => set({ request: { ...options, resolve } })),
  resolve: (ok) => {
    get().request?.resolve(ok);
    set({ request: null });
  },
}));

/** Imperative confirm dialog: `if (await confirm({ title })) doThing()`. Replaces window.confirm. */
export function useConfirm() {
  return useConfirmStore((state) => state.confirm);
}

/** Mounted once near the root (app/provider). Renders the single active confirm request. */
export function ConfirmDialog() {
  const request = useConfirmStore((state) => state.request);
  const resolve = useConfirmStore((state) => state.resolve);

  return (
    <AlertDialog
      open={request !== null}
      onOpenChange={(open) => {
        if (!open) {
          resolve(false);
        }
      }}
    >
      {request ? (
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>{request.title}</AlertDialogTitle>
            {request.description ? (
              <AlertDialogDescription>{request.description}</AlertDialogDescription>
            ) : null}
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel onClick={() => resolve(false)}>
              {request.cancelLabel ?? 'Cancel'}
            </AlertDialogCancel>
            <AlertDialogAction
              className={
                request.destructive ? cn(buttonVariants({ variant: 'destructive' })) : undefined
              }
              onClick={() => resolve(true)}
            >
              {request.confirmLabel ?? 'Confirm'}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      ) : null}
    </AlertDialog>
  );
}
