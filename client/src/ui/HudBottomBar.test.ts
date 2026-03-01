import { beforeEach, describe, expect, it, vi } from "vitest";
import { HudBottomBar, formatCoarseLocation } from "./HudBottomBar";
import type { ShipSnapshot } from "../types";

function mockMatchMedia(matches: boolean): void {
	Object.defineProperty(window, "matchMedia", {
		writable: true,
		value: vi.fn().mockReturnValue({ matches }),
	});
}

function sampleShip(overrides?: Partial<ShipSnapshot>): ShipSnapshot {
	return {
		shipId: 10,
		playerId: "player-1",
		sectorId: 4,
		x: 5_500_000,
		y: -3_500_000,
		velocityX: 2000,
		velocityY: -3000,
		heading: 0,
		credits: 500,
		cargoHoldUsed: 0,
		cargoHoldCapacity: 100,
		thrust: null,
		torque: null,
		...overrides,
	};
}

describe("HudBottomBar", () => {
	beforeEach(() => {
		document.body.innerHTML = "";
		mockMatchMedia(false);
	});

	it("mounts once and stays mounted through world updates", () => {
		const hud = new HudBottomBar(10);
		hud.mount(document.body);
		const firstRoot = document.querySelector(".hud-bottom-bar");
		expect(firstRoot).not.toBeNull();

		hud.update([sampleShip()], 4);
		hud.update([sampleShip({ x: 6_500_000 })], 4);
		hud.update([sampleShip({ x: 7_500_000 })], 4);

		const secondRoot = document.querySelector(".hud-bottom-bar");
		expect(secondRoot).toBe(firstRoot);
		hud.unmount();
	});

	it("triggers credits pulse only when value changes after first hydration", () => {
		const hud = new HudBottomBar(10);
		hud.mount(document.body);

		hud.update([sampleShip({ credits: 500 })], 4);
		const creditsEl = document.querySelector('[aria-live="polite"]') as HTMLElement;
		expect(creditsEl.classList.contains("pulse-subtle")).toBe(false);

		hud.update([sampleShip({ credits: 600 })], 4);
		expect(creditsEl.classList.contains("pulse-subtle")).toBe(true);
		hud.unmount();
	});

	it("triggers subtle hold pulse on deposit and emphatic pulse when full", () => {
		const hud = new HudBottomBar(10);
		hud.mount(document.body);

		hud.update([sampleShip({ cargoHoldUsed: 10, cargoHoldCapacity: 100 })], 4);
		const statusEls = document.querySelectorAll('[aria-live="polite"]');
		const holdEl = statusEls.item(1) as HTMLElement;

		hud.update([sampleShip({ cargoHoldUsed: 20, cargoHoldCapacity: 100 })], 4);
		expect(holdEl.classList.contains("pulse-subtle")).toBe(true);

		hud.update([sampleShip({ cargoHoldUsed: 100, cargoHoldCapacity: 100 })], 4);
		expect(holdEl.classList.contains("pulse-emphatic")).toBe(true);
		const holdBar = document.querySelector(".hud-hold-fill") as HTMLElement;
		expect(holdBar.classList.contains("hud-hold-fill-full")).toBe(true);
		hud.unmount();
	});

	it("suppresses pulse classes when prefers-reduced-motion is enabled", () => {
		mockMatchMedia(true);
		const hud = new HudBottomBar(10);
		hud.mount(document.body);

		hud.update([sampleShip({ credits: 100 })], 4);
		hud.update([sampleShip({ credits: 200 })], 4);

		const creditsEl = document.querySelector('[aria-live="polite"]') as HTMLElement;
		expect(creditsEl.classList.contains("pulse-subtle")).toBe(false);
		hud.unmount();
	});

	it("sets aria-live and role only for credits and hold, not for speed", () => {
		const hud = new HudBottomBar(10);
		hud.mount(document.body);
		hud.update([sampleShip()], 4);

		const polite = document.querySelectorAll('[aria-live="polite"]');
		expect(polite.length).toBe(2);
		expect(polite.item(0).getAttribute("role")).toBe("status");
		expect(polite.item(1).getAttribute("role")).toBe("status");

		const speedValue = Array.from(document.querySelectorAll("span")).find((el) =>
			el.textContent?.includes("m/s"),
		);
		expect(speedValue).toBeDefined();
		expect(speedValue?.hasAttribute("aria-live")).toBe(false);
		hud.unmount();
	});

	it("shows speed magnitude independent of heading sign", () => {
		const hud = new HudBottomBar(10);
		hud.mount(document.body);

		hud.update([
			sampleShip({
				heading: 0,
				velocityX: 0,
				velocityY: 1000,
			}),
		], 4);

		const speedValue = Array.from(document.querySelectorAll("span")).find((el) =>
			el.textContent?.includes("m/s"),
		);
		expect(speedValue?.textContent).toBe("1.0 m/s");
		hud.unmount();
	});

	it("renders coarse location and updates on coarse boundary crossing", () => {
		const hud = new HudBottomBar(10);
		hud.mount(document.body);

		hud.update([sampleShip({ x: -20_000_000, y: -20_000_000 })], 4);
		const firstLocation = Array.from(document.querySelectorAll("span"))
			.map((el) => el.textContent ?? "")
			.find((value) => value.startsWith("S4"));
		expect(firstLocation).toMatch(/^S4\s\d\d\s\d\d\s\d\d$/);

		hud.update([sampleShip({ x: 20_000_000, y: 20_000_000 })], 4);
		const secondLocation = Array.from(document.querySelectorAll("span"))
			.map((el) => el.textContent ?? "")
			.find((value) => value.startsWith("S4"));
		expect(secondLocation).toMatch(/^S4\s\d\d\s\d\d\s\d\d$/);
		expect(secondLocation).not.toBe(firstLocation);
		expect(secondLocation).not.toContain("20_000_000");
		hud.unmount();
	});

	it("renders fallback location from world sector even when local ship snapshot is missing", () => {
		const hud = new HudBottomBar(10);
		hud.mount(document.body);

		hud.update([], 9);

		const location = Array.from(document.querySelectorAll("span"))
			.map((el) => el.textContent ?? "")
			.find((value) => value.startsWith("S9"));
		expect(location).toBeDefined();
		expect(location).toMatch(/^S9\s\d\d\s\d\d\s\d\d$/);
		hud.unmount();
	});
});

describe("formatCoarseLocation", () => {
	it("never renders meter-level precision", () => {
		const text = formatCoarseLocation(7, 12_345_678, -22_222_222);
		expect(text).toMatch(/^S7\s\d\d\s\d\d\s\d\d$/);
		expect(text.includes("12345678")).toBe(false);
		expect(text.includes("-22222222")).toBe(false);
	});
});
