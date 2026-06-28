import { env } from '@/config/env';

/**
 * Vendor-agnostic error reporter. The app and the React Query cache callbacks talk to this interface; the
 * concrete backend (Sentry today) is swappable by replacing the adapter below. When `VITE_SENTRY_DSN` is
 * unset, the no-op default keeps production clean and the Sentry SDK fully tree-shaken (dynamic import).
 */
export interface Monitor {
  init: () => Promise<void> | void;
  captureException: (error: unknown, context?: Record<string, unknown>) => void;
  setUser: (user: { id?: string; email?: string } | null) => void;
}

const noop: Monitor = {
  init: () => {},
  captureException: (error, context) => {
    if (import.meta.env.DEV) {
      console.error('[monitoring]', error, context);
    }
  },
  setUser: () => {},
};

function createSentryMonitor(): Monitor {
  let sentry: typeof import('@sentry/react') | null = null;
  return {
    init: async () => {
      sentry = await import('@sentry/react');
      sentry.init({
        dsn: env.sentryDsn,
        environment: env.sentryEnvironment,
        release: env.appVersion,
        tracesSampleRate: 0.1,
      });
    },
    captureException: (error, context) => sentry?.captureException(error, { extra: context }),
    setUser: (user) => sentry?.setUser(user),
  };
}

/** No-op unless VITE_SENTRY_DSN is set. Swap `createSentryMonitor` to change vendor. */
export const monitoring: Monitor = env.sentryDsn ? createSentryMonitor() : noop;
