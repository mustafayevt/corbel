import { Button } from '@/components/ui/button';
import { type ColorMode, useColorModeStore } from '@/lib/color-mode';

const NEXT: Record<ColorMode, ColorMode> = { system: 'light', light: 'dark', dark: 'system' };
const LABEL: Record<ColorMode, string> = {
  system: 'System theme',
  light: 'Light theme',
  dark: 'Dark theme',
};

function ThemeIcon({ mode }: { mode: ColorMode }) {
  if (mode === 'dark') {
    return (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
        <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z" />
      </svg>
    );
  }
  if (mode === 'light') {
    return (
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
        <circle cx="12" cy="12" r="4" />
        <path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M4.93 19.07l1.41-1.41M17.66 6.34l1.41-1.41" />
      </svg>
    );
  }
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
      <rect x="2" y="4" width="20" height="14" rx="2" />
      <path d="M8 21h8M12 18v3" />
    </svg>
  );
}

/** Cycles system → light → dark. The label announces the current mode for screen readers. */
export function ThemeToggle() {
  const mode = useColorModeStore((state) => state.mode);
  const setMode = useColorModeStore((state) => state.setMode);

  return (
    <Button
      variant="ghost"
      size="icon"
      onClick={() => setMode(NEXT[mode])}
      aria-label={`${LABEL[mode]} (activate to switch)`}
      title={LABEL[mode]}
    >
      <ThemeIcon mode={mode} />
      <span className="sr-only">{LABEL[mode]}</span>
    </Button>
  );
}
