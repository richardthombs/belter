import { beforeEach, describe, expect, it, vi } from "vitest";
import { BackgroundLayer } from "./BackgroundLayer";

vi.mock("pixi.js", () => {
	class MockContainer {
		children: unknown[] = [];
		eventMode: string = "auto";
		interactiveChildren = true;

		addChild(child: unknown): void {
			this.children.push(child);
		}
	}

	class MockGraphics {
		clear(): this {
			return this;
		}
		circle(): this {
			return this;
		}
		fill(): this {
			return this;
		}
		moveTo(): this {
			return this;
		}
		lineTo(): this {
			return this;
		}
		stroke(): this {
			return this;
		}
	}

	return {
		Container: MockContainer,
		Graphics: MockGraphics,
	};
});

function setLocationSearch(search: string): void {
	const normalized = search.length === 0 ? "" : search.startsWith("?") ? search : `?${search}`;
	const url = normalized.length === 0 ? "/" : `/${normalized}`;
	window.history.replaceState({}, "", url);
}

describe("BackgroundLayer", () => {
	beforeEach(() => {
		setLocationSearch("");
		vi.restoreAllMocks();
	});

	it("is visual-only and does not capture pointer interactions", () => {
		const layer = new BackgroundLayer({
			developmentMode: true,
			treatmentOverride: "starfield",
			reducedMotionOverride: false,
		});

		expect(layer.eventMode).toBe("none");
		expect(layer.interactiveChildren).toBe(false);
	});

	it("produces deterministic starfield points for same camera + viewport", () => {
		const layer = new BackgroundLayer({
			developmentMode: true,
			treatmentOverride: "starfield",
			reducedMotionOverride: true,
		});

		layer.update({
			cameraX: 1_234_000,
			cameraY: -7_890_000,
			viewportWidth: 1280,
			viewportHeight: 720,
			deltaMs: 16,
		});

		const first = layer.getDebugState();

		layer.update({
			cameraX: 1_234_000,
			cameraY: -7_890_000,
			viewportWidth: 1280,
			viewportHeight: 720,
			deltaMs: 16,
		});

		const second = layer.getDebugState();

		expect(first.treatment).toBe("starfield");
		expect(first.starHashes).toEqual(second.starHashes);
		expect(first.starCount).toBeGreaterThan(0);
	});

	it("keeps starfield visible at large world coordinates", () => {
		const layer = new BackgroundLayer({
			developmentMode: true,
			treatmentOverride: "starfield",
			reducedMotionOverride: true,
		});

		layer.update({
			cameraX: 25_000_000,
			cameraY: -25_000_000,
			viewportWidth: 1280,
			viewportHeight: 720,
			deltaMs: 16,
		});

		expect(layer.getDebugState().starCount).toBeGreaterThan(0);
	});

	it("supports development-time treatment switch via query parameter", () => {
		setLocationSearch("bgRef=grid");

		const layer = new BackgroundLayer({
			developmentMode: true,
			reducedMotionOverride: false,
		});

		layer.update({
			cameraX: 0,
			cameraY: 0,
			viewportWidth: 800,
			viewportHeight: 600,
			deltaMs: 16,
		});

		const state = layer.getDebugState();
		expect(state.treatment).toBe("grid");
		expect(state.gridLineCount).toBeGreaterThan(0);
		expect(state.gridSubLineCount).toBeGreaterThan(0);
		expect(state.motionOffset).toBe(0);
	});

	it("accepts bgRef=stars alias for starfield", () => {
		setLocationSearch("bgRef=stars");

		const layer = new BackgroundLayer({
			developmentMode: true,
			reducedMotionOverride: false,
		});

		layer.update({
			cameraX: 0,
			cameraY: 0,
			viewportWidth: 800,
			viewportHeight: 600,
			deltaMs: 16,
		});

		expect(layer.getDebugState().treatment).toBe("starfield");
		expect(layer.getDebugState().starCount).toBeGreaterThan(0);
	});

	it("uses static/minimal motion behavior when reduced motion is enabled", () => {
		const layer = new BackgroundLayer({
			developmentMode: true,
			treatmentOverride: "starfield",
			reducedMotionOverride: true,
		});

		layer.update({
			cameraX: 500_000,
			cameraY: 900_000,
			viewportWidth: 1024,
			viewportHeight: 768,
			deltaMs: 16,
		});
		const first = layer.getDebugState();

		layer.update({
			cameraX: 500_000,
			cameraY: 900_000,
			viewportWidth: 1024,
			viewportHeight: 768,
			deltaMs: 1000,
		});
		const second = layer.getDebugState();

		expect(first.reducedMotion).toBe(true);
		expect(second.reducedMotion).toBe(true);
		expect(first.motionOffset).toBe(second.motionOffset);
	});
});
