import '@tanstack/react-query';

/**
 * Typed `meta.errorMessage` opt-out channel for the global QueryCache/MutationCache error handlers
 * (lib/react-query). Set `meta: { errorMessage: false }` on a query/mutation that surfaces its own
 * error inline (e.g. RHF field errors) to suppress the global toast; a string overrides the toast text.
 */
declare module '@tanstack/react-query' {
  interface Register {
    queryMeta: { errorMessage?: string | false };
    mutationMeta: { errorMessage?: string | false };
  }
}
