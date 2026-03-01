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
 *
 * Wire format note:
 *   The SignalR MessagePack protocol serialises C# record property names as-is
 *   (PascalCase). normalizeKeys() maps them to camelCase at the entry point so
 *   the rest of the client works with idiomatic TypeScript casing throughout.
 */
let ships: ShipSnapshot[] = [];
let asteroids: AsteroidSnapshot[] = [];
let timestamp = 0;
let sectorId = 0;

/** Recursively converts top-level and nested object keys from PascalCase to camelCase. */
function normalizeKeys<T>(obj: Record<string, unknown>): T {
    const out: Record<string, unknown> = {};
    for (const key of Object.keys(obj)) {
        const camel = key.charAt(0).toLowerCase() + key.slice(1);
        const val = obj[key];
        if (Array.isArray(val)) {
            out[camel] = val.map((item) =>
                item !== null && typeof item === "object"
                    ? normalizeKeys(item as Record<string, unknown>)
                    : item,
            );
        } else {
            out[camel] = val;
        }
    }
    return out as T;
}

export function apply(raw: WorldStateUpdate): void {
    const update = normalizeKeys<WorldStateUpdate>(
        raw as unknown as Record<string, unknown>,
    );
    sectorId = update.sectorId;
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

export function getSectorId(): number {
    return sectorId;
}

/** @deprecated use named exports; kept for backward compat */
export const WorldState = { apply, getShips, getAsteroids, getTimestamp, getSectorId };
