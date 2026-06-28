/// <reference types="vitest/config" />
import { fileURLToPath, URL } from 'node:url';
import tailwindcss from '@tailwindcss/vite';
import react from '@vitejs/plugin-react';
import { visualizer } from 'rollup-plugin-visualizer';
import { loadEnv } from 'vite';
import { compression } from 'vite-plugin-compression2';
import { defineConfig } from 'vitest/config';

// React Compiler runs as a Babel plugin. It memoizes components automatically, so we keep hand-written
// `useMemo`/`useCallback` to a minimum. Targeting React 19 (the default), no runtime package required.
const reactCompilerConfig = { target: '19' };

/**
 * One stable vendor chunk for all third-party code so app changes don't bust its long cache, while route
 * pages stay code-split (React.lazy). We intentionally do NOT sub-split the vendor: most deps (react-dom,
 * react-router, @tanstack, @radix, zustand, react-hook-form) consume React, and separating a React-using
 * library into a different chunk from React reintroduces the "React is undefined / useLayoutEffect" init-order
 * bug. Sentry is excluded (returns undefined) so Rollup keeps it in its own lazy chunk (loaded only with a DSN).
 */
function manualChunks(id: string): string | undefined {
  if (!id.includes('node_modules') || id.includes('@sentry')) {
    return undefined;
  }
  return 'vendor';
}

export default defineConfig(({ mode }) => {
  // `pnpm analyze` (vite build --mode analyze) emits dist/stats.html for bundle inspection. Driven off the
  // build mode rather than an inline env var so the script stays cross-platform (no `VAR=x cmd` shell syntax).
  const analyze = mode === 'analyze';
  // Load every env var (no `VITE_` filter) so the dev proxy target can be overridden privately.
  const env = loadEnv(mode, process.cwd(), '');
  const devProxyTarget = env.VITE_DEV_PROXY_TARGET || 'https://localhost:7080';

  // Dev server is plain HTTP: the browser only ever talks to this origin, and `/api` is forwarded to the
  // backend server-side (so there's no mixed content). Dev cookies are non-Secure (CookieAuth:Secure=false),
  // so HTTP is sufficient, and it keeps the Aspire dashboard's web link openable without a TLS cert.
  return {
    plugins: [
      react({ babel: { plugins: [['babel-plugin-react-compiler', reactCompilerConfig]] } }),
      tailwindcss(),
      // Build-time precompression: emit .gz and .br next to each asset (nginx serves them via *_static).
      compression({ algorithms: ['gzip'], threshold: 1024, deleteOriginalAssets: false }),
      compression({ algorithms: ['brotliCompress'], threshold: 1024, deleteOriginalAssets: false }),
      ...(analyze
        ? [visualizer({ filename: 'dist/stats.html', gzipSize: true, brotliSize: true })]
        : []),
    ],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url)),
      },
    },
    server: {
      port: 5173,
      strictPort: true,
      // Same-origin `/api` in dev: the browser talks to Vite, which forwards to the Kestrel HTTPS
      // endpoint. `secure: false` accepts the ASP.NET dev certificate; cookies flow because the
      // browser still sees a single origin (http://localhost:5173 — the dev server is plain HTTP).
      proxy: {
        '/api': {
          target: devProxyTarget,
          changeOrigin: true,
          secure: false,
        },
      },
    },
    preview: {
      port: 4173,
    },
    build: {
      target: 'es2022',
      // 'hidden' emits source maps WITHOUT the //# sourceMappingURL comment, so nginx never advertises them.
      // The Docker build strips *.map before serving; add a source-map upload to your error tracker (e.g.
      // Sentry) in CI if you want symbolicated production stack traces.
      sourcemap: 'hidden',
      chunkSizeWarningLimit: 600,
      rollupOptions: {
        output: { manualChunks },
      },
    },
    test: {
      globals: true,
      // happy-dom (not jsdom): its fetch/Request/Response/AbortSignal are mutually consistent and play well
      // with MSW + undici, avoiding jsdom's "signal is not an instance of AbortSignal" interop error. Its
      // default origin is http://localhost, matching the test API base below.
      environment: 'happy-dom',
      setupFiles: ['./src/testing/setup.ts'],
      css: true,
      // The app's default API base is empty (same-origin), which yields relative URLs like
      // `/api/auth/login`. Node's fetch can't parse a relative URL with no origin, so give the client an
      // absolute base in tests; MSW handlers use this same origin.
      env: { VITE_API_BASE_URL: 'http://localhost' },
      // Playwright owns `e2e/`; keep Vitest out of it.
      exclude: ['**/node_modules/**', '**/dist/**', 'e2e/**'],
      coverage: {
        provider: 'v8',
        reporter: ['text', 'html', 'lcov'],
        reportsDirectory: './coverage',
        include: ['src/**/*.{ts,tsx}'],
        exclude: [
          'src/types/**', // generated schema + ambient declarations
          'src/**/*.d.ts',
          'src/main.tsx',
          'src/testing/**',
          'src/**/*.test.{ts,tsx}',
          'src/vite-env.d.ts',
        ],
        // Floors set just under current coverage; ratchet up as breadth grows. CI fails below these.
        thresholds: { statements: 65, branches: 60, functions: 65, lines: 65 },
      },
    },
  };
});
