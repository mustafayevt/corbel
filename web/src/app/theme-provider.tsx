import { type ReactNode, useEffect } from 'react';
import { resolveColorMode, useColorModeStore } from '@/lib/color-mode';

/**
 * Toggles the `.dark` class on <html> — the hook the Tailwind `@custom-variant dark` (styles/index.css)
 * waits for, which activates the OKLCH dark token palette. Follows `prefers-color-scheme` live while in
 * 'system'. The initial class is set pre-React by the anti-flash script in index.html, so this only keeps
 * it in sync as the mode changes.
 */
export function ThemeProvider({ children }: { children: ReactNode }) {
  const mode = useColorModeStore((state) => state.mode);

  useEffect(() => {
    const apply = () =>
      document.documentElement.classList.toggle('dark', resolveColorMode(mode) === 'dark');
    apply();
    if (mode !== 'system') {
      return;
    }
    const media = window.matchMedia('(prefers-color-scheme: dark)');
    media.addEventListener('change', apply);
    return () => media.removeEventListener('change', apply);
  }, [mode]);

  return <>{children}</>;
}
