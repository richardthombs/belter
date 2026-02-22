import type {
    AsteroidSnapshot,
    ShipSnapshot,
    WorldStateUpdate,
} from "../types";

/**
 * WorldState — plain TypeScript singleton module.
 * Mutated directly by SignalR message handlers; consumed by PixiJS tick.
 * No reactive framework — no React/Vue/Svelte.
 *
 * Timestamp convention:
 *   REST API:    ISO 8601 UTC strings
 *   SignalR game messages: Unix milliseconds (integer)
 */
let ships: ShipSnapshot[] = [];
let asteroids: AsteroidSnapshot[] = [];
let timestamp = 0;

export function apply(update: WorldStateUpdate): void {
    ships = update.ships;
    asteroids = update.asteroids;
    timestamp = update.timestamp;
}

export function getShips(): readonly ShipSnapshot[] {
    return ships;
}

export function getAsteroids(): readonly AsteroidSnapshot[] {
    return asteroids;
}

export function getTimestamp(): number {
    return timestamp;
}

/** @deprecated use named exports; kept for backward compat */
export const WorldState = { apply, getShips, getAsteroids, getTimestamp };
