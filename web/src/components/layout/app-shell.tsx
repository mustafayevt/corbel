import { Outlet } from 'react-router-dom';
import { Navbar, type NavbarProps } from './navbar';

/** Chrome for the authenticated app: sticky navbar over the routed page content. Presentational and
 *  props-driven — the auth wiring lives in the app layer (see app/routes/app/dashboard-layout). */
export function AppShell({ user, onLogout }: NavbarProps) {
  return (
    <div className="flex min-h-svh flex-col bg-background">
      {/* Skip link: lets keyboard/screen-reader users jump past the navbar straight to the page content. */}
      <a
        href="#main"
        className="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-4 focus:z-20 focus:rounded-md focus:bg-background focus:px-3 focus:py-2 focus:shadow"
      >
        Skip to content
      </a>
      <Navbar user={user} onLogout={onLogout} />
      <main id="main" tabIndex={-1} className="flex-1">
        <Outlet />
      </main>
    </div>
  );
}
