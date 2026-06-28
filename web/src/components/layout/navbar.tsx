import { Link } from 'react-router-dom';
import { ThemeToggle } from '@/components/theme-toggle';
import { Button } from '@/components/ui/button';
import { env } from '@/config/env';

/** Minimal identity shape the navbar needs — kept local so this shared component never imports a feature. */
export interface NavbarUser {
  email: string;
  displayName?: string | null;
}

export interface NavbarProps {
  user: NavbarUser | null;
  onLogout: () => void;
}

export function Navbar({ user, onLogout }: NavbarProps) {
  const label = user?.displayName?.trim() || user?.email || 'Account';

  return (
    <header className="sticky top-0 z-10 border-b border-border bg-background/80 backdrop-blur">
      <div className="mx-auto flex h-14 w-full max-w-5xl items-center justify-between gap-4 px-4">
        <Link to="/" className="flex items-center gap-2 font-semibold tracking-tight">
          <span className="grid size-7 place-items-center rounded-md bg-primary text-sm text-primary-foreground">
            {env.appName.charAt(0)}
          </span>
          {env.appName}
        </Link>

        <div className="flex items-center gap-3">
          <span className="hidden max-w-[12rem] truncate text-sm text-muted-foreground sm:inline">
            {label}
          </span>
          <ThemeToggle />
          <Button variant="outline" size="sm" onClick={onLogout}>
            Sign out
          </Button>
        </div>
      </div>
    </header>
  );
}
