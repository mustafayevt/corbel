import checkFile from 'eslint-plugin-check-file';
import importPlugin from 'eslint-plugin-import';
import reactHooks from 'eslint-plugin-react-hooks';
import tseslint from 'typescript-eslint';

/**
 * ESLint is intentionally scoped to the three things Biome cannot express; Biome remains the primary
 * formatter + linter (see biome.json). Keeping the two tools' concerns disjoint avoids duplicate diagnostics:
 *   1. Architectural import boundaries (import/no-restricted-paths) — enforce unidirectional flow.
 *   2. kebab-case file + folder naming (check-file).
 *   3. React Compiler / rules-of-hooks diagnostics (react-hooks) — Biome has no compiler analysis.
 * `exhaustive-deps` is deliberately delegated to Biome (useExhaustiveDependencies) to avoid double-reporting.
 */
export default tseslint.config(
  {
    ignores: [
      'dist',
      'playwright-report',
      'test-results',
      'coverage',
      'node_modules',
      'src/types/schema.d.ts',
    ],
  },

  // Parser for all source TS/TSX (needed by the import + react-hooks rules).
  {
    files: ['src/**/*.{ts,tsx}'],
    languageOptions: {
      parser: tseslint.parser,
      parserOptions: { ecmaFeatures: { jsx: true }, sourceType: 'module' },
    },
  },

  // (1) Architectural boundaries: shared (components/hooks/lib/config/types) → features → app.
  // Test files and the testing harness are exempt — they legitimately reach across layers.
  {
    files: ['src/**/*.{ts,tsx}'],
    ignores: ['src/**/*.test.{ts,tsx}', 'src/testing/**'],
    plugins: { import: importPlugin },
    settings: { 'import/resolver': { typescript: { project: './tsconfig.app.json' } } },
    rules: {
      'import/no-restricted-paths': [
        'error',
        {
          zones: [
            // (a) No cross-feature imports — features compose only at the app layer. NOTE: this list is not
            // auto-derived, so add a zone for each new feature you create or its cross-feature imports go unchecked.
            {
              target: './src/features/auth',
              from: './src/features',
              except: ['./auth'],
              message: 'Cross-feature import: features/auth must not import another feature.',
            },
            {
              target: './src/features/notes',
              from: './src/features',
              except: ['./notes'],
              message: 'Cross-feature import: features/notes must not import another feature.',
            },
            // (b) Shared layers must not import from features/* or app/*.
            {
              target: [
                './src/components',
                './src/hooks',
                './src/lib',
                './src/config',
                './src/types',
              ],
              from: ['./src/features', './src/app'],
              message:
                'Unidirectional flow: shared layers must not import from features/* or app/*.',
            },
            // (c) Features must not import from the app layer.
            {
              target: './src/features',
              from: './src/app',
              message: 'Unidirectional flow: features must not import from the app layer.',
            },
          ],
        },
      ],
      'import/no-cycle': ['error', { maxDepth: Number.POSITIVE_INFINITY }],
    },
  },

  // (2) kebab-case files + folders. ignoreMiddleExtensions covers *.test.tsx / *.schemas.ts patterns.
  {
    files: ['src/**/*.{ts,tsx}'],
    plugins: { 'check-file': checkFile },
    rules: {
      'check-file/filename-naming-convention': [
        'error',
        { '**/*.{ts,tsx}': 'KEBAB_CASE' },
        { ignoreMiddleExtensions: true },
      ],
      'check-file/folder-naming-convention': ['error', { 'src/**/': 'KEBAB_CASE' }],
    },
  },

  // (3) React Compiler / rules-of-hooks. exhaustive-deps is owned by Biome, so turn it off here.
  {
    files: ['src/**/*.{ts,tsx}'],
    plugins: { 'react-hooks': reactHooks },
    rules: {
      ...reactHooks.configs['recommended-latest'].rules,
      'react-hooks/exhaustive-deps': 'off',
    },
  },
);
