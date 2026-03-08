// Vector polygon with cacheAsTexture for performance.
import { Container, Graphics } from "pixi.js";
import type { AsteroidSnapshot } from "../../types";
import { toScreen } from "../worldScale";

function buildPolygonPoints(snapshot: AsteroidSnapshot): number[] {
    const points: number[] = [];
    for (let i = 0; i < snapshot.vertexCount; i++) {
        const angle =
            (i / snapshot.vertexCount) * Math.PI * 2 + snapshot.rotationOffset;
        const r =
            toScreen(snapshot.radius) *
            (0.8 + (0.2 * ((snapshot.asteroidId * 17 + i * 31) % 100)) / 100);
        points.push(Math.cos(angle) * r, Math.sin(angle) * r);
    }
    return points;
}

export class AsteroidRenderer extends Container {
    private readonly highlight: Graphics;

    constructor(snapshot: AsteroidSnapshot, onSelect: (asteroidId: number) => void) {
        super();
        const g = new Graphics();
        const points = buildPolygonPoints(snapshot);
        g.poly(points).fill(0x000000).stroke({ color: 0xffffff, width: 1 });
        this.addChild(g);

        const highlight = new Graphics();
        const radius = toScreen(snapshot.radius) * 1.3;
        highlight.circle(0, 0, radius).stroke({ color: 0x22d3ee, width: 2, alpha: 0.95 });
        highlight.visible = false;
        this.addChild(highlight);
        this.highlight = highlight;

        this.eventMode = "static";
        this.cursor = "pointer";
        this.on("pointertap", () => onSelect(snapshot.asteroidId));

    }

    update(snapshot: AsteroidSnapshot): void {
        this.position.set(toScreen(snapshot.x), toScreen(snapshot.y));
    }

    setSelected(selected: boolean): void {
        this.highlight.visible = selected;
    }
}
