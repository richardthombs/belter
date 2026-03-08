import type { AsteroidSnapshot, ShipSnapshot } from "../types";

export const interactionRangesMm = {
	scan: 2_000_000,
	mining: 900_000,
} as const;

export type InteractionActionKey =
	| "set-course"
	| "scan"
	| "mine"
	| "drill"
	| "claim";

export interface InteractionActionState {
	key: InteractionActionKey;
	label: string;
	enabled: boolean;
}

export interface SelectedObjectViewState {
	objectType: "asteroid";
	objectId: number;
	objectName: string;
	iconToken: string;
	distanceMm: number;
	actions: InteractionActionState[];
	showGetCloserHint: boolean;
}

interface SelectionState {
	objectType: "asteroid";
	objectId: number;
	revealedActions: Set<InteractionActionKey>;
	lastDistanceMm: number;
}

let selection: SelectionState | null = null;

export function selectObject(objectType: "asteroid", objectId: number): void {
	if (selection && selection.objectType === objectType && selection.objectId === objectId) {
		return;
	}

	selection = {
		objectType,
		objectId,
		revealedActions: new Set<InteractionActionKey>(["set-course"]),
		lastDistanceMm: Number.POSITIVE_INFINITY,
	};
}

export function clearSelection(): void {
	selection = null;
}

export function getSelectedObjectId(): number | null {
	return selection?.objectId ?? null;
}

export function getSelectionViewState(): SelectedObjectViewState | null {
	if (!selection) {
		return null;
	}

	const inScanRange = selection.lastDistanceMm <= interactionRangesMm.scan;
	const inMiningRange = selection.lastDistanceMm <= interactionRangesMm.mining;

	const actions: InteractionActionState[] = [
		{ key: "set-course", label: "Set Course", enabled: true },
	];

	if (selection.revealedActions.has("scan")) {
		actions.push({ key: "scan", label: "Scan", enabled: inScanRange });
	}

	if (selection.revealedActions.has("mine")) {
		actions.push({ key: "mine", label: "Mine", enabled: inMiningRange });
	}

	if (selection.revealedActions.has("drill")) {
		actions.push({ key: "drill", label: "Drill", enabled: inMiningRange });
	}

	if (selection.revealedActions.has("claim")) {
		actions.push({ key: "claim", label: "Claim", enabled: inMiningRange });
	}

	return {
		objectType: selection.objectType,
		objectId: selection.objectId,
		objectName: `Asteroid #${selection.objectId}`,
		iconToken: "◌",
		distanceMm: selection.lastDistanceMm,
		actions,
		showGetCloserHint: !inScanRange,
	};
}

export function updateSelectionFromWorld(
	ships: readonly ShipSnapshot[],
	asteroids: readonly AsteroidSnapshot[],
	localShipId: number,
): void {
	if (!selection) {
		return;
	}

	const localShip = ships.find((ship) => ship.shipId === localShipId);
	const selectedAsteroid = asteroids.find((asteroid) => asteroid.asteroidId === selection?.objectId);
	if (!localShip || !selectedAsteroid) {
		clearSelection();
		return;
	}

	selection.lastDistanceMm = Math.round(
		Math.hypot(selectedAsteroid.x - localShip.x, selectedAsteroid.y - localShip.y),
	);

	if (selection.lastDistanceMm <= interactionRangesMm.scan) {
		selection.revealedActions.add("scan");
	}

	if (selection.lastDistanceMm <= interactionRangesMm.mining) {
		selection.revealedActions.add("mine");
		selection.revealedActions.add("drill");
		selection.revealedActions.add("claim");
	}
}

export function resetObjectSelectionStateForTests(): void {
	clearSelection();
}
