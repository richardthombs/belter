// Renderer — PixiJS v8 Application wrapper.
// Note: Application.init() is async in v8 (breaking change from v7).
import { Application } from "pixi.js";
import { BackgroundLayer } from "./layers/BackgroundLayer";
import { WorldLayer } from "./layers/WorldLayer";
import { EffectsLayer } from "./layers/EffectsLayer";
import { UiLayer } from "./layers/UiLayer";

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

    start(): void {
        this.app.ticker.add((ticker) => this.tick(ticker.deltaTime));
    }

    private tick(_delta: number): void {
        const shipPos = this.worldLayer.update();
        if (shipPos) {
            this.worldLayer.position.set(
                this.app.screen.width / 2 - shipPos.x,
                this.app.screen.height / 2 - shipPos.y,
            );
        }
    }

    getWorldLayer(): WorldLayer {
        return this.worldLayer;
    }
}
