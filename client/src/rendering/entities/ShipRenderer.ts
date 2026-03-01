import { Container, Graphics } from "pixi.js";
import type { ShipSnapshot } from "../../types";
import { toScreen } from "../worldScale";

export class ShipRenderer extends Container {
    constructor() {
        super();
        const g = new Graphics();
        g.moveTo(0, -12)
            .lineTo(8, 10)
            .lineTo(-8, 10)
            .closePath()
            .fill(0x00ff88);
        this.addChild(g);
    }

    update(snapshot: ShipSnapshot): void {
        this.position.set(toScreen(snapshot.x), toScreen(snapshot.y));
        this.rotation = snapshot.heading;
    }
}
