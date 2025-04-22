import * as PIXI from "pixi.js";

// Create PixiJS Application
// Create PixiJS Application that fills the window
const app = new PIXI.Application({
    resizeTo: window,
    backgroundColor: 0x000000,
});
document.getElementById("game").appendChild(app.view);

// Handle window resize
window.addEventListener("resize", () => {
    app.renderer.resize(window.innerWidth, window.innerHeight);
});

// Asteroid rendering
let asteroidGraphics = [];

// Camera parameters (centered on middle of map)
const WORLD_WIDTH = 1_000_000;
const WORLD_HEIGHT = 1_000_000;
let camera = {
    x: WORLD_WIDTH / 2,
    y: WORLD_HEIGHT / 2,
};

// WebSocket connection
const ws = new WebSocket("ws://localhost:5000/ws");
ws.onopen = () => {
    console.log("WebSocket connected");
};
ws.onmessage = (msg) => {
    // Remove old asteroids
    asteroidGraphics.forEach((g) => app.stage.removeChild(g));
    asteroidGraphics = [];
    // Parse asteroids
    let asteroids = [];
    try {
        asteroids = JSON.parse(msg.data);
    } catch (e) {
        console.error("Invalid asteroid data", e);
        return;
    }
    asteroids.forEach((a) => {
        const g = new PIXI.Graphics();
        g.lineStyle(2, 0xffffff); // White outline
        // Draw polygon (relative to 0,0)
        if (a.Polygon && a.Polygon.length === 24) {
            g.moveTo(a.Polygon[0], a.Polygon[1]);
            for (let i = 2; i < 24; i += 2) {
                g.lineTo(a.Polygon[i], a.Polygon[i + 1]);
            }
            g.closePath();
        }
        // No fill
        // Position asteroid in screen space (centered camera)
        g.x = a.Position.X - camera.x + app.renderer.width / 2;
        g.y = a.Position.Y - camera.y + app.renderer.height / 2;
        app.stage.addChild(g);
        asteroidGraphics.push(g);
    });
};
ws.onclose = () => {
    console.log("WebSocket closed");
};
