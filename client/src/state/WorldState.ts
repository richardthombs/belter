/**
 * WorldState — plain TypeScript singleton module.
 * Mutated directly by SignalR message handlers; consumed by PixiJS tick.
 * No reactive framework — no React/Vue/Svelte.
 *
 * Timestamp convention:
 *   REST API:    ISO 8601 UTC strings
 *   SignalR game messages: Unix milliseconds (integer)
 */
export const WorldState = {
};
