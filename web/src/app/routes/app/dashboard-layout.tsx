import { AppShell } from '@/components/layout/app-shell';
import { useAuth } from '@/features/auth/hooks/use-auth';

/**
 * App-layer container that supplies the authenticated chrome with its data. Composing the feature hook
 * here (rather than inside `AppShell`) keeps the shared layout component free of any feature import,
 * satisfying the unidirectional dependency rule.
 */
export function DashboardLayout() {
  const { user, logout } = useAuth();
  return <AppShell user={user} onLogout={() => void logout()} />;
}
