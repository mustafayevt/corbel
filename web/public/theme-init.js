// Anti-flash theme bootstrap. Loaded as an external same-origin script (not inline) so it satisfies the
// strict `script-src 'self'` CSP. Runs before first paint to set the color-mode class, so a dark-preference
// reload doesn't flash light. Mirrors lib/color-mode (storage key + zustand/persist envelope shape).
(() => {
  try {
    const stored = localStorage.getItem('corbel-color-mode');
    const mode = stored ? JSON.parse(stored).state.mode : null;
    const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
    if (mode === 'dark' || ((!mode || mode === 'system') && prefersDark)) {
      document.documentElement.classList.add('dark');
    }
  } catch {
    /* localStorage unavailable — fall back to the default light theme. */
  }
})();
