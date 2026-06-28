import { setupServer } from 'msw/node';
import { handlers } from './handlers';

/**
 * Shared MSW server, seeded with default happy-path handlers. Tests override per-case via `server.use(...)`
 * (runtime handlers take precedence); unhandled requests fail (onUnhandledRequest: 'error' in setup).
 */
export const server = setupServer(...handlers);
