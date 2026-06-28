import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterAll, afterEach, beforeAll } from 'vitest';
import { useNotificationsStore } from '@/components/ui/notifications/notifications-store';
import { registerAuthBridge } from '@/features/auth/api/auth-bridge';
import { resetBootstrapAuthForTests } from '@/features/auth/hooks/use-bootstrap-auth';
import { useAuthStore } from '@/features/auth/stores/auth-store';
import { queryClient } from '@/lib/react-query';
import { server } from './mocks/server';

// Wire the api-client ↔ auth bridge once, mirroring app startup, so the transport's refresh/replay path
// exercises the real production wiring under test.
registerAuthBridge();

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));

afterEach(() => {
  server.resetHandlers();
  cleanup();
  // Reset shared singletons so tests don't leak auth state or cached queries into each other.
  queryClient.clear();
  useAuthStore.setState({ accessToken: null, user: null, hasBootstrapped: false });
  useNotificationsStore.getState().clear();
  resetBootstrapAuthForTests();
});

afterAll(() => server.close());
