export type PulseIntensity = "subtle" | "emphatic";

function prefersReducedMotion(): boolean {
	return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
}

export function canAnimatePulse(): boolean {
	return !prefersReducedMotion();
}

export function triggerPulse(
	element: HTMLElement,
	intensity: PulseIntensity,
): void {
	if (!canAnimatePulse()) {
		return;
	}

	const pulseClass = intensity === "emphatic" ? "pulse-emphatic" : "pulse-subtle";
	element.classList.remove("pulse-subtle", "pulse-emphatic");
	element.classList.add(pulseClass);
	window.setTimeout(() => {
		element.classList.remove(pulseClass);
	}, 460);
}
