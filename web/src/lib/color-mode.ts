import { create } from 'zustand';
import { persist } from 'zustand/middleware';

export type ColorMode = 'light' | 'dark' | 'system';

/** Shared with the anti-flash inline script in index.html — keep the two in sync. */
export const COLOR_MODE_KEY = 'corbel-color-mode';

interface ColorModeState {
  mode: ColorMode;
  setMode: (mode: ColorMode) => void;
}

export const useColorModeStore = create<ColorModeState>()(
  persist((set) => ({ mode: 'system', setMode: (mode) => set({ mode }) }), {
    name: COLOR_MODE_KEY,
  }),
);

/** Resolve 'system' against the OS preference. */
export function resolveColorMode(mode: ColorMode): 'light' | 'dark' {
  if (mode === 'system') {
    return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  }
  return mode;
}
