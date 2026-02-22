// Vector polygon with cacheAsTexture for performance.
import { Container, Graphics } from "pixi.js";
import type { AsteroidSnapshot } from "../../types";

function buildPolygonPoints(snapshot: AsteroidSnapshot): number[] {
    const points: number[] = [];
    for (let i = 0; i < snapshot.vertexCount; i++) {
        const angle =
            (i / snapshot.vertexCount) * Math.PI * 2 + snapshot.rotationOffset;
        const r =
            snapshot.radius *
            (0.8 + (0.2 * ((snapshot.asteroidId * 17 + i * 31) % 100)) / 100);
        points.push(Math.cos(angle) * r, Math.sin(angle) * r);
    }
    return points;
}

export class AsteroidRenderer extends Container {
    constructor(snapshot: AsteroidSnapshot) {
        super();
        const g = new Graphics();
        const points = buildPolygonPoints(snapshot);
        g.poly(points).fill(0x888888);
        this.addChild(g);
        this.cacheAsTexture(true);
    }

    update(snapshot: AsteroidSnapshot): void {
        this.position.set(snapshot.x, snapshot.y);
    }
}
