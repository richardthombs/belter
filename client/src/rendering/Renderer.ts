// Renderer — PixiJS v8 Application wrapper.
// Note: Application.init() is async in v8 (breaking change from v7).
import { Application } from "pixi.js";
import { BackgroundLayer } from "./layers/BackgroundLayer";
import { WorldLayer } from "./layers/WorldLayer";
import { EffectsLayer } from "./layers/EffectsLayer";
import { UiLayer } from "./layers/UiLayer";
import { toScreen } from "./worldScale";

export class Renderer {
	private app: Application = new Application();
	private worldLayer!: WorldLayer;

	async init(canvas: HTMLCanvasElement): Promise<void> {
		await this.app.init({
			canvas,
			resizeTo: window,
			backgroundColor: 0x0a0a1a,
		});

		const backgroundLayer = new BackgroundLayer();
		this.worldLayer = new WorldLayer();
		const effectsLayer = new EffectsLayer();
		const uiLayer = new UiLayer();

		this.app.stage.addChild(backgroundLayer);
		this.app.stage.addChild(this.worldLayer);
		this.app.stage.addChild(effectsLayer);
		this.app.stage.addChild(uiLayer);
	}

	setLocalShipId(shipId: number): void {
		this.worldLayer.setLocalShipId(shipId);
	}

	initCameraAt(x: number, y: number): void {
		// Pre-position the camera before the first tick to avoid a 1-frame snap from origin.
		this.worldLayer.position.set(
			this.app.screen.width / 2 - toScreen(x),
			this.app.screen.height / 2 - toScreen(y),
		);
	}

	start(): void {
		this.app.ticker.add((ticker) => this.tick(ticker.deltaTime));
	}

	private tick(_delta: number): void {
		const shipPos = this.worldLayer.update();
		if (shipPos) {
			this.worldLayer.position.set(
				this.app.screen.width / 2 - toScreen(shipPos.x),
				this.app.screen.height / 2 - toScreen(shipPos.y),
			);
		}
	}

	getWorldLayer(): WorldLayer {
		return this.worldLayer;
	}
}
