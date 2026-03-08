import { beforeEach, describe, expect, it, vi } from "vitest";
import { ContextualPanel } from "./ContextualPanel";
import type { SelectedObjectViewState } from "../state/ObjectSelectionState";

function mockMatchMedia(matches: boolean): void {
	Object.defineProperty(window, "matchMedia", {
		writable: true,
		value: vi.fn().mockReturnValue({ matches }),
	});
}

function state(overrides?: Partial<SelectedObjectViewState>): SelectedObjectViewState {
	return {
		objectType: "asteroid",
		objectId: 9,
		objectName: "Asteroid #9",
		iconToken: "◌",
		distanceMm: 2_500_000,
		actions: [{ key: "set-course", label: "Set Course", enabled: true }],
		showGetCloserHint: true,
		...overrides,
	};
}

describe("ContextualPanel", () => {
	beforeEach(() => {
		document.body.innerHTML = "";
		mockMatchMedia(false);
	});

	it("renders role/aria label and updates object metadata", () => {
		const panel = new ContextualPanel(() => {});
		panel.mount(document.body);
		panel.render(state());

		const region = document.querySelector('[role="complementary"]') as HTMLElement;
		expect(region).not.toBeNull();
		expect(region.getAttribute("aria-label")).toBe("Asteroid #9 actions");
		expect(region.textContent).toContain("Asteroid #9");
		expect(region.textContent).toContain("Distance:");
		expect(region.textContent).toContain("Get closer for more");
		panel.unmount();
	});

	it("invokes close callback from close button and Escape", () => {
		const onClose = vi.fn();
		const panel = new ContextualPanel(onClose);
		panel.mount(document.body);
		panel.render(state());

		const closeButton = document.querySelector(".contextual-panel-close") as HTMLButtonElement;
		closeButton.click();
		expect(onClose).toHaveBeenCalledTimes(1);

		document.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape" }));
		expect(onClose).toHaveBeenCalledTimes(2);
		panel.unmount();
	});

	it("traps focus inside the panel while open", () => {
		const outside = document.createElement("button");
		outside.textContent = "Outside";
		document.body.appendChild(outside);
		outside.focus();

		const panel = new ContextualPanel(() => {});
		panel.mount(document.body);
		panel.render(
			state({
				actions: [
					{ key: "set-course", label: "Set Course", enabled: true },
					{ key: "scan", label: "Scan", enabled: true },
				],
			}),
		);

		const closeButton = document.querySelector(".contextual-panel-close") as HTMLButtonElement;
		expect(document.activeElement).toBe(closeButton);

		closeButton.dispatchEvent(new KeyboardEvent("keydown", { key: "Tab", bubbles: true }));
		const setCourse = Array.from(document.querySelectorAll(".contextual-panel-action")).at(0) as HTMLButtonElement;
		expect(document.activeElement).toBe(setCourse);

		setCourse.dispatchEvent(
			new KeyboardEvent("keydown", { key: "Tab", shiftKey: true, bubbles: true }),
		);
		expect(document.activeElement).toBe(closeButton);

		panel.render(null);
		expect(document.activeElement).toBe(outside);
		panel.unmount();
	});

	it("applies touch-safe action and close sizing classes", () => {
		const panel = new ContextualPanel(() => {});
		panel.mount(document.body);
		panel.render(state());

		const closeButton = document.querySelector(".contextual-panel-close") as HTMLButtonElement;
		const actionButton = document.querySelector(".contextual-panel-action") as HTMLButtonElement;

		expect(closeButton.className).toContain("h-11");
		expect(closeButton.className).toContain("w-11");
		expect(actionButton.className).toContain("min-h-11");
		panel.unmount();
	});
});
