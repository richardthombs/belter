import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { TouchInput } from "./TouchInput";

function mockMatchMedia(matches: boolean) {
	Object.defineProperty(window, "matchMedia", {
		writable: true,
		value: vi.fn().mockReturnValue({ matches }),
	});
}

function setMaxTouchPoints(n: number) {
	Object.defineProperty(navigator, "maxTouchPoints", {
		writable: true,
		configurable: true,
		value: n,
	});
}

describe("TouchInput", () => {
	beforeEach(() => {
		mockMatchMedia(false);
		setMaxTouchPoints(1);
		document.body.innerHTML = "";
	});

	afterEach(() => {
		vi.restoreAllMocks();
	});

	// ── DOM mounting ──────────────────────────────────────────────────────────

	describe("DOM mounting", () => {
		it("mounts joystick zone to body on touch-capable device", () => {
			const input = new TouchInput(() => { });
			expect(document.body.querySelector(".joystick-zone")).not.toBeNull();
			input.dispose();
		});

		it("does NOT mount on non-touch device (maxTouchPoints === 0)", () => {
			setMaxTouchPoints(0);
			const input = new TouchInput(() => { });
			expect(document.body.querySelector(".joystick-zone")).toBeNull();
			input.dispose();
		});

		it("nests ring inside zone and nub inside ring", () => {
			const input = new TouchInput(() => { });
			const zone = document.body.querySelector(".joystick-zone")!;
			expect(zone.querySelector(".joystick-ring")).not.toBeNull();
			expect(zone.querySelector(".joystick-nub")).not.toBeNull();
			input.dispose();
		});
	});

	// ── prefers-reduced-motion ────────────────────────────────────────────────

	describe("prefers-reduced-motion", () => {
		it("sets nub transition to 'none' when reduced motion is enabled", () => {
			mockMatchMedia(true);
			const input = new TouchInput(() => { });
			const nub = document.body.querySelector(".joystick-nub") as HTMLElement;
			expect(nub.style.transition).toBe("none");
			input.dispose();
		});

		it("sets nub transition to ease-out when reduced motion is disabled", () => {
			mockMatchMedia(false);
			const input = new TouchInput(() => { });
			const nub = document.body.querySelector(".joystick-nub") as HTMLElement;
			expect(nub.style.transition).toBe("transform 0.05s ease-out");
			input.dispose();
		});
	});

	// ── dead zone and normalisation ───────────────────────────────────────────

	describe("normalisation and dead zone", () => {
		let input: TouchInput;
		let received: { thrust: number; torque: number }[];

		function seedTouch(centerX = 60, centerY = 60) {
			(input as any).activeTouch = 0;
			(input as any).centerX = centerX;
			(input as any).centerY = centerY;
		}

		beforeEach(() => {
			received = [];
			input = new TouchInput((thrust, torque) =>
				received.push({ thrust, torque }),
			);
		});

		afterEach(() => {
			input.dispose();
		});

		it("returns (0, 0) for displacement within dead zone (< 5px)", () => {
			seedTouch();
			(input as any).processTouchPosition(63, 60); // dx=3 < deadZone=5
			expect(received[0]).toEqual({ thrust: 0, torque: 0 });
		});

		it("returns thrust=1 for full upward push (dominant Y axis)", () => {
			seedTouch();
			(input as any).processTouchPosition(60, 24); // dy=-36 = -maxRadius
			expect(received[0].thrust).toBe(1);
			expect(received[0].torque).toBe(0);
		});

		it("returns thrust=-1 for full downward push", () => {
			seedTouch();
			(input as any).processTouchPosition(60, 96); // dy=+36 = maxRadius
			expect(received[0].thrust).toBe(-1);
			expect(received[0].torque).toBe(0);
		});

		it("returns torque=1 for full rightward push (dominant X axis)", () => {
			seedTouch();
			(input as any).processTouchPosition(96, 60); // dx=+36 = maxRadius
			expect(received[0].torque).toBe(1);
			expect(received[0].thrust).toBe(0);
		});

		it("returns torque=-1 for full leftward push", () => {
			seedTouch();
			(input as any).processTouchPosition(24, 60); // dx=-36 = -maxRadius
			expect(received[0].torque).toBe(-1);
			expect(received[0].thrust).toBe(0);
		});

		it("clamps thrust to 1 for over-travel beyond maxRadius", () => {
			seedTouch();
			(input as any).processTouchPosition(60, 0); // dy=-60, >> maxRadius
			expect(received[0].thrust).toBe(1);
		});

		it("returns partial thrust proportional to displacement", () => {
			seedTouch();
			// dy=-18 = maxRadius/2 → normalised 0.5 (past dead zone)
			(input as any).processTouchPosition(60, 42);
			expect(received[0].thrust).toBeCloseTo(0.5);
			expect(received[0].torque).toBe(0);
		});

		// ── dominant-axis ─────────────────────────────────────────────────────

		it("dominant axis: thrust wins when absDy > absDx", () => {
			seedTouch();
			// dy=-20 (up), dx=10 (right): absDy(20) > absDx(10) → thrust
			(input as any).processTouchPosition(70, 40);
			expect(received[0].thrust).toBeGreaterThan(0);
			expect(received[0].torque).toBe(0);
		});

		it("dominant axis: torque wins when absDx > absDy", () => {
			seedTouch();
			// dx=20 (right), dy=-5 (up): absDx(20) > absDy(5) → torque
			(input as any).processTouchPosition(80, 55);
			expect(received[0].torque).toBeGreaterThan(0);
			expect(received[0].thrust).toBe(0);
		});

		it("tie-break at 45°: thrust wins (absDy >= absDx condition)", () => {
			seedTouch();
			// dx=20, dy=-20: absDy === absDx → thrust wins via >=
			(input as any).processTouchPosition(80, 40);
			expect(received[0].thrust).toBeGreaterThan(0);
			expect(received[0].torque).toBe(0);
		});
	});

	// ── touch tracking — single touch only ───────────────────────────────────

	describe("single-touch enforcement", () => {
		it("ignores a second touchstart while a touch is already active", () => {
			const received: { thrust: number; torque: number }[] = [];
			const input = new TouchInput((t, r) => received.push({ thrust: t, torque: r }));

			(input as any).activeTouch = 0;
			(input as any).centerX = 60;
			(input as any).centerY = 60;

			// Second touchstart fires — should be ignored (activeTouch !== null guard)
			(input as any).handleTouchStart(
				new Event("touchstart") as any,
			);
			// activeTouch should still be 0, no new callback fired for start
			expect((input as any).activeTouch).toBe(0);

			input.dispose();
		});
	});

	// ── dispose lifecycle ─────────────────────────────────────────────────────

	describe("dispose()", () => {
		it("removes zone DOM element from body", () => {
			const input = new TouchInput(() => { });
			expect(document.body.querySelector(".joystick-zone")).not.toBeNull();
			input.dispose();
			expect(document.body.querySelector(".joystick-zone")).toBeNull();
		});

		it("removes window event listeners (touchmove no longer fires onChange)", () => {
			const received: number[] = [];
			const input = new TouchInput((t) => received.push(t));

			(input as any).activeTouch = 0;
			(input as any).centerX = 60;
			(input as any).centerY = 60;

			input.dispose();

			// After dispose, the window touchmove listener should be gone
			const spy = vi.spyOn(input as any, "processTouchPosition");
			window.dispatchEvent(new Event("touchmove"));
			expect(spy).not.toHaveBeenCalled();
		});

		it("is safe to call dispose on a non-touch device (no DOM mounted)", () => {
			setMaxTouchPoints(0);
			const input = new TouchInput(() => { });
			expect(() => input.dispose()).not.toThrow();
		});
	});
});
