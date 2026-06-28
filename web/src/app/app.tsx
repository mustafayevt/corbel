import { RouterProvider } from 'react-router-dom';
import { registerAuthBridge } from '@/features/auth/api/auth-bridge';
import { useBootstrapAuth } from '@/features/auth/hooks/use-bootstrap-auth';
import { AppProvider } from './provider';
import { router } from './router';

// Wire the shared api-client to the auth feature before the first request can fire (module load order:
// this runs when main.tsx imports App, ahead of the bootstrap effect and any router data fetch).
registerAuthBridge();

function AppRouter() {
  // Kick off the one-shot silent refresh; ProtectedRoute holds a splash until it resolves.
  useBootstrapAuth();
  return <RouterProvider router={router} />;
}

export function App() {
  return (
    <AppProvider>
      <AppRouter />
    </AppProvider>
  );
}
