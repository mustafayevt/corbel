import { create } from 'zustand';

export type NotificationVariant = 'info' | 'success' | 'warning' | 'error';

export interface Notification {
  id: string;
  variant: NotificationVariant;
  title: string;
  description?: string;
  /** ms before auto-dismiss; omit for sticky (errors). Consumed by Radix Toast's `duration`. */
  duration?: number;
}

interface NotificationsState {
  notifications: Notification[];
  add: (notification: Omit<Notification, 'id'>) => string;
  dismiss: (id: string) => void;
  clear: () => void;
}

/**
 * Holds the active toasts only — timing/pausing is owned by Radix Toast (see ui/toast), so we deliberately
 * do NOT run timers here. Mirrors the auth store's imperative-alias pattern so non-React callers (the React
 * Query cache callbacks) can push notifications.
 */
export const useNotificationsStore = create<NotificationsState>((set) => ({
  notifications: [],
  add: (notification) => {
    const id = crypto.randomUUID();
    set((state) => ({ notifications: [...state.notifications, { id, ...notification }] }));
    return id;
  },
  dismiss: (id) =>
    set((state) => ({ notifications: state.notifications.filter((n) => n.id !== id) })),
  clear: () => set({ notifications: [] }),
}));

/** Imperative access for non-React modules (the React Query cache onError handlers). */
export const notificationsStore = useNotificationsStore;
