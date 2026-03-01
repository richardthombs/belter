import { describe, expect, it, vi } from "vitest";

const stageChildren: unknown[] = [];

vi.mock("pixi.js", () => {
	class MockApplication {
		public stage = {
			addChild: (child: unknown) => {
				stageChildren.push(child);
			},
		};
		public screen = { width: 1280, height: 720 };
		public ticker = {
			add: vi.fn(),
		};

		async init(): Promise<void> {
			return;
		}
	}

	return {
		Application: MockApplication,
	};
});

vi.mock("./layers/BackgroundLayer", () => ({
	BackgroundLayer: class BackgroundLayer {
		update(): void {}
	},
}));

vi.mock("./layers/WorldLayer", () => ({
	WorldLayer: class WorldLayer {
		position = { set: vi.fn() };
		update(): { x: number; y: number } | null {
			return null;
		}
		setLocalShipId(): void {}
	},
}));

vi.mock("./layers/EffectsLayer", () => ({
	EffectsLayer: class EffectsLayer {},
}));

vi.mock("./layers/UiLayer", () => ({
	UiLayer: class UiLayer {},
}));

describe("Renderer", () => {
	it("keeps rendering layer order: Background -> World -> Effects -> Ui", async () => {
		stageChildren.length = 0;
		const { Renderer } = await import("./Renderer");
		const renderer = new Renderer();

		const canvas = document.createElement("canvas");
		await renderer.init(canvas);

		expect(stageChildren.length).toBe(4);
		expect(stageChildren[0]?.constructor.name).toBe("BackgroundLayer");
		expect(stageChildren[1]?.constructor.name).toBe("WorldLayer");
		expect(stageChildren[2]?.constructor.name).toBe("EffectsLayer");
		expect(stageChildren[3]?.constructor.name).toBe("UiLayer");
	});
});
