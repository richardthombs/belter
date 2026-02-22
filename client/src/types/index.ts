/**
 * Shared TypeScript types — manually mirrored from BelterLife.Shared contracts.
 * Keep minimal; divergence risk mitigated by small surface area.
 * Code generation (NSwag) deferred to post-MVP.
 *
 * Timestamp conventions:
 *   REST responses: ISO 8601 UTC string
 *   SignalR game messages: Unix milliseconds (number)
 */

export interface PlayerDto {
    id: string;
    username: string;
}

export interface ShipSnapshot {
    shipId: number;
    playerId: string;
    x: number;
    y: number;
    velocityX: number;
    velocityY: number;
    heading: number;
    /** Populated ~once/s for reconciliation; null all other ticks. */
    thrust?: number | null;
    /** Populated ~once/s for reconciliation; null all other ticks. */
    torque?: number | null;
}

export interface AsteroidSnapshot {
    asteroidId: number;
    x: number;
    y: number;
    radius: number;
    vertexCount: number;
    rotationOffset: number;
}

export interface WorldStateUpdate {
    sectorId: number;
    timestamp: number;
    ships: ShipSnapshot[];
    asteroids: AsteroidSnapshot[];
}

export interface SpawnResponse {
    sectorId: number;
    shipId: number;
    spawnX: number;
    spawnY: number;
}

/** Mirrors BelterLife.Shared.Contracts.Hubs.InputEvent.
 *  Client → Server via SignalR SendInput hub method.
 *  Wire format uses PascalCase (ContractlessStandardResolver): Thrust, Torque, Brake.
 *  Thrust: 1 = main engines (forward), -1 = retro thrusters, 0 = off.
 *  Torque: 1 = rotate right, -1 = rotate left, 0 = off. */
export interface InputEvent {
    thrust: number;
    torque: number;
    brake: boolean;
}
