import type { InteractionActionState, SelectedObjectViewState } from "../state/ObjectSelectionState";

function formatDistance(distanceMm: number): string {
	const meters = distanceMm / 1000;
	if (meters >= 1000) {
		return `${(meters / 1000).toFixed(1)} km`;
	}
	return `${Math.round(meters).toLocaleString()} m`;
}

export class ContextualPanel {
	private root: HTMLElement | null = null;
	private panel: HTMLElement | null = null;
	private headerTitle: HTMLElement | null = null;
	private distance: HTMLElement | null = null;
	private actions: HTMLElement | null = null;
	private helper: HTMLElement | null = null;
	private content: HTMLElement | null = null;
	private open = false;
	private selectedObjectId: number | null = null;
	private previousVisibleActions = new Set<string>();
	private restoreFocusTo: HTMLElement | null = null;

	private readonly closeHandler: () => void;
	private readonly onKeyDownBound: (event: KeyboardEvent) => void;

	constructor(onCloseRequested: () => void) {
		this.closeHandler = onCloseRequested;
		this.onKeyDownBound = (event: KeyboardEvent) => this.onKeyDown(event);
	}

	mount(container: HTMLElement): void {
		if (this.root) {
			return;
		}

		const root = document.createElement("aside");
		root.className = "contextual-panel-root pointer-events-none fixed top-0 right-0 bottom-24 z-40 flex items-stretch";
		root.setAttribute("aria-hidden", "true");

		const panel = document.createElement("section");
		panel.className =
			"contextual-panel pointer-events-auto h-full w-[min(24rem,92vw)] translate-x-full border-l border-zinc-700/70 bg-zinc-900/92 text-zinc-100 shadow-2xl transition-transform duration-200 ease-out";
		panel.setAttribute("role", "complementary");
		panel.setAttribute("aria-label", "Object actions");
		panel.setAttribute("tabindex", "-1");

		const shell = document.createElement("div");
		shell.className = "flex h-full flex-col";

		const header = document.createElement("header");
		header.className = "flex items-start justify-between gap-3 border-b border-zinc-700/70 px-4 py-4";

		const titleWrap = document.createElement("div");
		titleWrap.className = "min-w-0";

		const title = document.createElement("h2");
		title.className = "truncate text-base font-semibold";
		title.textContent = "";

		const distance = document.createElement("p");
		distance.className = "mt-1 text-sm text-zinc-300";
		distance.textContent = "";

		titleWrap.appendChild(title);
		titleWrap.appendChild(distance);

		const close = document.createElement("button");
		close.type = "button";
		close.className =
			"contextual-panel-close inline-flex h-11 min-h-11 w-11 min-w-11 items-center justify-center rounded-md border border-zinc-600 bg-zinc-800 text-lg font-semibold text-zinc-100 hover:bg-zinc-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyan-400";
		close.setAttribute("aria-label", "Close object actions");
		close.textContent = "×";
		close.addEventListener("click", () => this.closeHandler());

		header.appendChild(titleWrap);
		header.appendChild(close);

		const content = document.createElement("div");
		content.className = "contextual-panel-content flex-1 overflow-y-auto px-4 py-4";

		const actions = document.createElement("div");
		actions.className = "flex flex-col gap-2";

		const helper = document.createElement("p");
		helper.className = "mt-4 text-sm text-zinc-400";
		helper.textContent = "Get closer for more";

		content.appendChild(actions);
		content.appendChild(helper);

		shell.appendChild(header);
		shell.appendChild(content);
		panel.appendChild(shell);
		root.appendChild(panel);
		container.appendChild(root);

		this.root = root;
		this.panel = panel;
		this.headerTitle = title;
		this.distance = distance;
		this.actions = actions;
		this.helper = helper;
		this.content = content;
	}

	unmount(): void {
		document.removeEventListener("keydown", this.onKeyDownBound);
		this.root?.remove();
		this.root = null;
		this.panel = null;
		this.headerTitle = null;
		this.distance = null;
		this.actions = null;
		this.helper = null;
		this.content = null;
		this.open = false;
		this.selectedObjectId = null;
		this.previousVisibleActions.clear();
		this.restoreFocusTo = null;
	}

	render(state: SelectedObjectViewState | null): void {
		if (!this.root || !this.panel || !this.headerTitle || !this.distance || !this.actions || !this.helper) {
			return;
		}

		if (!state) {
			this.hide();
			return;
		}

		const isDifferentSelection = this.selectedObjectId !== null && this.selectedObjectId !== state.objectId;
		this.selectedObjectId = state.objectId;

		this.root.setAttribute("aria-hidden", "false");
		this.panel.classList.remove("translate-x-full");
		this.panel.setAttribute("aria-label", `${state.objectName} actions`);
		this.headerTitle.textContent = `${state.iconToken} ${state.objectName}`;
		this.distance.textContent = `Distance: ${formatDistance(state.distanceMm)}`;

		this.renderActions(state.actions);
		this.helper.classList.toggle("hidden", !state.showGetCloserHint);

		if (!this.open) {
			this.open = true;
			this.restoreFocusTo =
				document.activeElement instanceof HTMLElement ? document.activeElement : null;
			document.addEventListener("keydown", this.onKeyDownBound);
			this.focusFirstTarget();
		}

		if (isDifferentSelection && this.content && !window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
			this.content.classList.remove("contextual-panel-crossfade");
			void this.content.offsetWidth;
			this.content.classList.add("contextual-panel-crossfade");
		}
	}

	private renderActions(actions: InteractionActionState[]): void {
		if (!this.actions) {
			return;
		}

		const nextActionKeys = new Set(actions.map((action) => action.key));
		this.actions.innerHTML = "";

		for (const action of actions) {
			const button = document.createElement("button");
			button.type = "button";
			button.textContent = action.label;
			button.className =
				"contextual-panel-action inline-flex min-h-11 w-full items-center justify-start rounded-md border border-zinc-600 bg-zinc-800 px-4 text-left text-sm font-medium text-zinc-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-cyan-400 disabled:cursor-not-allowed disabled:border-zinc-700 disabled:bg-zinc-900 disabled:text-zinc-500";
			button.disabled = !action.enabled;

			const isNewlyVisible = !this.previousVisibleActions.has(action.key);
			if (isNewlyVisible && !window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
				button.classList.add("contextual-panel-action-unlock");
			}

			this.actions.appendChild(button);
		}

		this.previousVisibleActions = nextActionKeys;
	}

	private hide(): void {
		if (!this.root || !this.panel) {
			return;
		}

		if (this.open) {
			document.removeEventListener("keydown", this.onKeyDownBound);
			this.restoreFocusTo?.focus();
		}

		this.open = false;
		this.selectedObjectId = null;
		this.previousVisibleActions.clear();
		this.root.setAttribute("aria-hidden", "true");
		this.panel.classList.add("translate-x-full");
	}

	private focusFirstTarget(): void {
		if (!this.panel) {
			return;
		}

		const focusableTargets = this.getFocusableTargets();
		if (focusableTargets.length > 0) {
			focusableTargets[0].focus();
			return;
		}

		this.panel.focus();
	}

	private onKeyDown(event: KeyboardEvent): void {
		if (!this.open || !this.panel) {
			return;
		}

		if (event.key === "Escape") {
			event.preventDefault();
			this.closeHandler();
			return;
		}

		if (event.key !== "Tab") {
			return;
		}

		const targets = this.getFocusableTargets();
		if (targets.length === 0) {
			event.preventDefault();
			this.panel.focus();
			return;
		}

		const active = document.activeElement as HTMLElement | null;
		const currentIndex = active ? targets.indexOf(active) : -1;
		const nextIndex = event.shiftKey
			? currentIndex <= 0
				? targets.length - 1
				: currentIndex - 1
			: currentIndex >= targets.length - 1
				? 0
				: currentIndex + 1;

		event.preventDefault();
		targets[nextIndex].focus();
	}

	private getFocusableTargets(): HTMLElement[] {
		if (!this.panel) {
			return [];
		}

		const nodes = this.panel.querySelectorAll<HTMLElement>(
			'button:not([disabled]), [href], [tabindex]:not([tabindex="-1"]), input:not([disabled]), select:not([disabled]), textarea:not([disabled])',
		);
		return Array.from(nodes);
	}
}
