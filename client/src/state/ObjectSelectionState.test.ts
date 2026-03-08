import { beforeEach, describe, expect, it } from "vitest";
import {
	clearSelection,
	getSelectionViewState,
	resetObjectSelectionStateForTests,
	selectObject,
	updateSelectionFromWorld,
} from "./ObjectSelectionState";
import type { AsteroidSnapshot, ShipSnapshot } from "../types";

function ship(x: number, y: number): ShipSnapshot {
	return {
		shipId: 7,
		playerId: "player-1",
		sectorId: 1,
		x,
		y,
		velocityX: 0,
		velocityY: 0,
		heading: 0,
		credits: 0,
		cargoHoldUsed: 0,
		cargoHoldCapacity: 100,
	};
}

function asteroid(x: number, y: number): AsteroidSnapshot {
	return {
		asteroidId: 10,
		x,
		y,
		radius: 50_000,
		vertexCount: 8,
		rotationOffset: 0,
	};
}

describe("ObjectSelectionState", () => {
	beforeEach(() => {
		resetObjectSelectionStateForTests();
	});

	it("shows far-range panel with only Set Course before scan gate", () => {
		selectObject("asteroid", 10);
		updateSelectionFromWorld([ship(0, 0)], [asteroid(3_000_000, 0)], 7);

		const view = getSelectionViewState();
		expect(view).not.toBeNull();
		expect(view?.actions.map((a) => a.key)).toEqual(["set-course"]);
		expect(view?.showGetCloserHint).toBe(true);
	});

	it("reveals Scan at scan range and greys it out when moving away", () => {
		selectObject("asteroid", 10);
		updateSelectionFromWorld([ship(0, 0)], [asteroid(1_900_000, 0)], 7);

		const nearView = getSelectionViewState();
		expect(nearView?.actions.map((a) => a.key)).toEqual(["set-course", "scan"]);
		expect(nearView?.actions.find((a) => a.key === "scan")?.enabled).toBe(true);

		updateSelectionFromWorld([ship(0, 0)], [asteroid(3_100_000, 0)], 7);
		const farAgain = getSelectionViewState();
		expect(farAgain?.actions.map((a) => a.key)).toEqual(["set-course", "scan"]);
		expect(farAgain?.actions.find((a) => a.key === "scan")?.enabled).toBe(false);
	});

	it("reveals mining actions at mining range", () => {
		selectObject("asteroid", 10);
		updateSelectionFromWorld([ship(0, 0)], [asteroid(850_000, 0)], 7);

		const view = getSelectionViewState();
		expect(view?.actions.map((a) => a.key)).toEqual([
			"set-course",
			"scan",
			"mine",
			"drill",
			"claim",
		]);
		expect(view?.showGetCloserHint).toBe(false);
	});

	it("keeps selection when re-tapping same object", () => {
		selectObject("asteroid", 10);
		updateSelectionFromWorld([ship(0, 0)], [asteroid(1_800_000, 0)], 7);

		selectObject("asteroid", 10);
		const view = getSelectionViewState();
		expect(view?.actions.map((a) => a.key)).toEqual(["set-course", "scan"]);
	});

	it("clears when selected object disappears from world state", () => {
		selectObject("asteroid", 10);
		updateSelectionFromWorld([ship(0, 0)], [asteroid(2_000_000, 0)], 7);

		updateSelectionFromWorld([ship(0, 0)], [], 7);
		expect(getSelectionViewState()).toBeNull();
		clearSelection();
	});
});
