import type { ShipSnapshot } from "../types";
import { triggerPulse } from "./PulseIndicator";

const halfSectorMm = 25_000_000;

function clamp(value: number, min: number, max: number): number {
	return Math.max(min, Math.min(max, value));
}

export function formatCoarseLocation(sectorId: number, x: number, y: number): string {
	const xMeters = clamp(Math.floor((x + halfSectorMm) / 1000), 0, 49_999);
	const yMeters = clamp(Math.floor((y + halfSectorMm) / 1000), 0, 49_999);

	const withinSquareE = xMeters % 10_000;
	const withinSquareN = yMeters % 10_000;

	const pair1 = `${Math.floor(withinSquareE / 1_000)}${Math.floor(withinSquareN / 1_000)}`;
	const pair2 = `${Math.floor((withinSquareE % 1_000) / 100)}${Math.floor((withinSquareN % 1_000) / 100)}`;
	const pair3 = `${Math.floor((withinSquareE % 100) / 10)}${Math.floor((withinSquareN % 100) / 10)}`;

	return `S${sectorId} ${pair1} ${pair2} ${pair3}`;
}

function toSpeedMetersPerSecond(ship: ShipSnapshot): number {
	const mmPerSecond = Math.hypot(ship.velocityX, ship.velocityY);
	return mmPerSecond / 1000;
}

export class HudBottomBar {
	private readonly localShipId: number;
	private root: HTMLDivElement | null = null;
	private creditsValueEl: HTMLSpanElement | null = null;
	private holdValueEl: HTMLSpanElement | null = null;
	private holdBarEl: HTMLDivElement | null = null;
	private speedValueEl: HTMLSpanElement | null = null;
	private locationValueEl: HTMLSpanElement | null = null;
	private previousCredits: number | null = null;
	private previousHoldUsed: number | null = null;

	constructor(localShipId: number) {
		this.localShipId = localShipId;
	}

	mount(container: HTMLElement): void {
		if (this.root) {
			return;
		}

		const root = document.createElement("div");
		root.className = "hud-bottom-bar fixed bottom-0 left-0 right-0 z-50 pointer-events-none";

		const panel = document.createElement("div");
		panel.className =
			"mx-3 mb-3 rounded-lg border border-zinc-700/70 bg-zinc-900/70 px-4 py-3 text-zinc-100 backdrop-blur-sm";

		const grid = document.createElement("div");
		grid.className = "grid grid-cols-2 gap-3 text-xs sm:grid-cols-4";

		const credits = this.createMetric("Credits", true);
		const hold = this.createMetric("Hold", true);
		const speed = this.createMetric("Speed", false);
		const location = this.createMetric("Location", false);

		this.creditsValueEl = credits.value;
		this.holdValueEl = hold.value;
		this.speedValueEl = speed.value;
		this.locationValueEl = location.value;

		const holdBarTrack = document.createElement("div");
		holdBarTrack.className = "mt-1 h-1.5 w-full rounded bg-zinc-700/80";
		const holdBar = document.createElement("div");
		holdBar.className = "hud-hold-fill h-full rounded bg-cyan-300 transition-all duration-150";
		holdBar.style.width = "0%";
		holdBarTrack.appendChild(holdBar);
		hold.wrapper.appendChild(holdBarTrack);
		this.holdBarEl = holdBar;

		grid.appendChild(credits.wrapper);
		grid.appendChild(hold.wrapper);
		grid.appendChild(speed.wrapper);
		grid.appendChild(location.wrapper);

		panel.appendChild(grid);
		root.appendChild(panel);
		container.appendChild(root);
		this.root = root;
	}

	unmount(): void {
		this.root?.remove();
		this.root = null;
		this.creditsValueEl = null;
		this.holdValueEl = null;
		this.holdBarEl = null;
		this.speedValueEl = null;
		this.locationValueEl = null;
		this.previousCredits = null;
		this.previousHoldUsed = null;
	}

	update(ships: readonly ShipSnapshot[], worldSectorId: number): void {
		if (!this.root || !this.creditsValueEl || !this.holdValueEl || !this.holdBarEl || !this.speedValueEl || !this.locationValueEl) {
			return;
		}

		const ship = ships.find((s) => s.shipId === this.localShipId);
		const displaySectorId = ship?.sectorId && ship.sectorId > 0 ? ship.sectorId : worldSectorId;
		if (displaySectorId > 0) {
			this.locationValueEl.textContent = formatCoarseLocation(displaySectorId, ship?.x ?? 0, ship?.y ?? 0);
		}

		if (!ship) {
			return;
		}

		this.updateCredits(ship.credits);
		this.updateHold(ship.cargoHoldUsed, ship.cargoHoldCapacity);

		const speedMps = toSpeedMetersPerSecond(ship);
		this.speedValueEl.textContent = `${speedMps.toFixed(1)} m/s`;
	}

	private updateCredits(credits: number): void {
		if (!this.creditsValueEl) {
			return;
		}

		this.creditsValueEl.textContent = credits.toLocaleString();
		if (this.previousCredits !== null && credits !== this.previousCredits) {
			triggerPulse(this.creditsValueEl, "subtle");
		}
		this.previousCredits = credits;
	}

	private updateHold(used: number, capacity: number): void {
		if (!this.holdValueEl || !this.holdBarEl) {
			return;
		}

		const normalizedCapacity = capacity > 0 ? capacity : 1;
		const percent = clamp((used / normalizedCapacity) * 100, 0, 100);
		const roundedPercent = Math.round(percent);

		this.holdValueEl.textContent = `${roundedPercent}%`;
		this.holdBarEl.style.width = `${percent}%`;

		const isFull = roundedPercent >= 100;
		this.holdBarEl.classList.toggle("hud-hold-fill-full", isFull);

		if (this.previousHoldUsed !== null && used > this.previousHoldUsed) {
			triggerPulse(this.holdValueEl, isFull ? "emphatic" : "subtle");
		}

		this.previousHoldUsed = used;
	}

	private createMetric(label: string, announce: boolean): { wrapper: HTMLDivElement; value: HTMLSpanElement } {
		const wrapper = document.createElement("div");
		wrapper.className = "min-w-0";

		const labelEl = document.createElement("div");
		labelEl.className = "text-zinc-400";
		labelEl.textContent = label;

		const valueEl = document.createElement("span");
		valueEl.className = "block truncate text-sm font-semibold text-zinc-100";
		if (announce) {
			valueEl.setAttribute("role", "status");
			valueEl.setAttribute("aria-live", "polite");
		}
		valueEl.textContent = "—";

		wrapper.appendChild(labelEl);
		wrapper.appendChild(valueEl);
		return { wrapper, value: valueEl };
	}
}
