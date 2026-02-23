export type TouchChangeCallback = (thrust: number, torque: number) => void;

export class TouchInput {
	private zone: HTMLDivElement;
	private ring: HTMLDivElement;
	private nub: HTMLDivElement;

	private readonly maxRadius = 36; // (120 - 48) / 2
	private readonly deadZone = 5;
	private reducedMotion: boolean;

	private activeTouch: number | null = null;
	private centerX = 0;
	private centerY = 0;

	private readonly onChange: TouchChangeCallback;

	private readonly onTouchStart: (e: TouchEvent) => void;
	private readonly onTouchMove: (e: TouchEvent) => void;
	private readonly onTouchEnd: (e: TouchEvent) => void;

	constructor(onChange: TouchChangeCallback) {
		this.onChange = onChange;
		this.reducedMotion = window.matchMedia(
			"(prefers-reduced-motion: reduce)",
		).matches;

		// Build DOM
		this.zone = document.createElement("div");
		this.zone.className = "joystick-zone";
		Object.assign(this.zone.style, {
			position: "fixed",
			bottom: "32px",
			left: "32px",
			width: "120px",
			height: "120px",
			borderRadius: "50%",
			touchAction: "none",
			zIndex: "100",
		});

		this.ring = document.createElement("div");
		this.ring.className = "joystick-ring";
		Object.assign(this.ring.style, {
			position: "absolute",
			inset: "0",
			borderRadius: "50%",
			border: "2px solid rgba(255,255,255,0.4)",
			background: "rgba(255,255,255,0.05)",
		});

		this.nub = document.createElement("div");
		this.nub.className = "joystick-nub";
		Object.assign(this.nub.style, {
			width: "48px",
			height: "48px",
			borderRadius: "50%",
			background: "rgba(255,255,255,0.6)",
			position: "absolute",
			top: "50%",
			left: "50%",
			transform: "translate(-50%, -50%)",
			transition: this.reducedMotion ? "none" : "transform 0.05s ease-out",
		});

		this.ring.appendChild(this.nub);
		this.zone.appendChild(this.ring);
		document.body.appendChild(this.zone);

		// Bind event handlers
		this.onTouchStart = this.handleTouchStart.bind(this);
		this.onTouchMove = this.handleTouchMove.bind(this);
		this.onTouchEnd = this.handleTouchEnd.bind(this);

		this.zone.addEventListener("touchstart", this.onTouchStart, {
			passive: false,
		});
		window.addEventListener("touchmove", this.onTouchMove, {
			passive: false,
		});
		window.addEventListener("touchend", this.onTouchEnd);
		window.addEventListener("touchcancel", this.onTouchEnd);
	}

	private handleTouchStart(e: TouchEvent): void {
		e.preventDefault();
		// Only track one touch at a time
		if (this.activeTouch !== null) return;

		const touch = e.changedTouches[0];
		this.activeTouch = touch.identifier;

		const rect = this.zone.getBoundingClientRect();
		this.centerX = rect.left + rect.width / 2;
		this.centerY = rect.top + rect.height / 2;

		this.processTouchPosition(touch.clientX, touch.clientY);
	}

	private handleTouchMove(e: TouchEvent): void {
		if (this.activeTouch === null) return;
		e.preventDefault();

		for (let i = 0; i < e.changedTouches.length; i++) {
			const touch = e.changedTouches[i];
			if (touch.identifier === this.activeTouch) {
				this.processTouchPosition(touch.clientX, touch.clientY);
				return;
			}
		}
	}

	private handleTouchEnd(e: TouchEvent): void {
		if (this.activeTouch === null) return;

		for (let i = 0; i < e.changedTouches.length; i++) {
			if (e.changedTouches[i].identifier === this.activeTouch) {
				this.activeTouch = null;
				this._updateNub(0, 0);
				this.onChange(0, 0);
				return;
			}
		}
	}

	private processTouchPosition(clientX: number, clientY: number): void {
		const rawDx = clientX - this.centerX;
		const rawDy = clientY - this.centerY;

		const dist = Math.sqrt(rawDx * rawDx + rawDy * rawDy);
		const clampedDist = Math.min(dist, this.maxRadius);

		let offsetX = 0;
		let offsetY = 0;

		if (dist > 0) {
			offsetX = (rawDx / dist) * clampedDist;
			offsetY = (rawDy / dist) * clampedDist;
		}

		// Dominant axis: whichever axis has larger displacement drives output;
		// the minor axis is zeroed. This prevents simultaneous thrust + rotation
		// from accidental diagonal drags.
		const absDx = Math.abs(rawDx);
		const absDy = Math.abs(rawDy);
		const thrust = absDy >= absDx ? this.applyDeadZoneAndNormalise(-rawDy) : 0;
		const torque = absDx > absDy ? this.applyDeadZoneAndNormalise(rawDx) : 0;

		this._updateNub(offsetX, offsetY);
		this.onChange(thrust, torque);
	}

	private applyDeadZoneAndNormalise(displacement: number): number {
		const abs = Math.abs(displacement);
		if (abs < this.deadZone) return 0;
		const clamped = Math.min(abs, this.maxRadius);
		const normalised = (clamped / this.maxRadius) * Math.sign(displacement);
		return Math.max(-1, Math.min(1, normalised));
	}

	_updateNub(offsetX: number, offsetY: number): void {
		this.nub.style.transform = `translate(calc(-50% + ${offsetX}px), calc(-50% + ${offsetY}px))`;
	}

	dispose(): void {
		this.zone.removeEventListener("touchstart", this.onTouchStart);
		window.removeEventListener("touchmove", this.onTouchMove);
		window.removeEventListener("touchend", this.onTouchEnd);
		window.removeEventListener("touchcancel", this.onTouchEnd);
		this.zone.remove();
	}
}
