// Runs from the repo root (the git root, a .NET solution dir). Frontend tooling lives in web/, so we run
// web's own Biome on the staged web files. lint-staged passes ABSOLUTE paths to function tasks; we rewrite
// them to web-relative so `pnpm -C web exec biome` (cwd = web) resolves them against web/biome.json.
//
// Backend (*.cs) formatting/analysis is deliberately NOT gated here — `dotnet format` loads the whole solution
// and is too slow for a commit hook. CI enforces it instead (`dotnet format --verify-no-changes`).
const toWebRelative = (files) => files.map((f) => f.replace(/^.*\/web\//, '')).join(' ');

export default {
  'web/**/*.{ts,tsx,js,jsx,json,css}': (files) =>
    `pnpm -C web exec biome check --write --no-errors-on-unmatched ${toWebRelative(files)}`,
  // A single project-wide typecheck when any TS changed (function ignores the file list).
  'web/**/*.{ts,tsx}': () => 'pnpm -C web run typecheck',
};
