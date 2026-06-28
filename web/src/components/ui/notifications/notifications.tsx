import {
  Toast,
  ToastClose,
  ToastDescription,
  ToastTitle,
  ToastViewport,
} from '@/components/ui/toast';
import { useNotificationsStore } from './notifications-store';

/**
 * Renders one Radix `<Toast>` per stored notification plus the viewport. Mounted once inside
 * `<ToastProvider>` (app/provider). Radix owns auto-dismiss timing via `duration` (errors are sticky:
 * Infinity) and pauses on hover/focus; removal happens when Radix reports the toast closed.
 */
export function Notifications() {
  const notifications = useNotificationsStore((state) => state.notifications);
  const dismiss = useNotificationsStore((state) => state.dismiss);

  return (
    <>
      {notifications.map((notification) => (
        <Toast
          key={notification.id}
          variant={notification.variant}
          duration={notification.duration ?? Number.POSITIVE_INFINITY}
          type={notification.variant === 'error' ? 'foreground' : 'background'}
          onOpenChange={(open) => {
            if (!open) {
              dismiss(notification.id);
            }
          }}
        >
          <div className="flex-1">
            <ToastTitle>{notification.title}</ToastTitle>
            {notification.description ? (
              <ToastDescription>{notification.description}</ToastDescription>
            ) : null}
          </div>
          <ToastClose
            aria-label="Dismiss"
            className="text-muted-foreground transition-colors hover:text-foreground"
          >
            ×
          </ToastClose>
        </Toast>
      ))}
      <ToastViewport />
    </>
  );
}
