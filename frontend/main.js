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

// Game state
let asteroids = [];
let asteroidGraphics = new Map(); // Map of asteroid ID to graphics

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

// Track currently pressed keys
const relevantKeys = [
    'KeyW', 'KeyA', 'KeyS', 'KeyD', // WASD
    'ArrowUp', 'ArrowLeft', 'ArrowDown', 'ArrowRight'
];
const pressedKeys = new Set();

window.addEventListener('keydown', (e) => {
    if (relevantKeys.includes(e.code)) {
        pressedKeys.add(e.code);
    }
});
window.addEventListener('keyup', (e) => {
    if (relevantKeys.includes(e.code)) {
        pressedKeys.delete(e.code);
    }
});

// Send all currently pressed keys every 33ms (30fps), but only send empty once
let sentEmpty = false;
setInterval(() => {
    if (ws.readyState !== WebSocket.OPEN) return;
    const keysArray = Array.from(pressedKeys);
    if (keysArray.length > 0) {
        ws.send(JSON.stringify({ type: 'keys', keys: keysArray }));
        sentEmpty = false;
    } else if (!sentEmpty) {
        ws.send(JSON.stringify({ type: 'keys', keys: [] }));
        sentEmpty = true;
    }
}, 33);

// Reset sentEmpty when a key is pressed again
window.addEventListener('keydown', (e) => {
    if (relevantKeys.includes(e.code)) {
        pressedKeys.add(e.code);
        sentEmpty = false;
    }
});

// Cache for asteroid graphics data
const graphicsCache = new Map();

// Create initial graphics for an asteroid
function createAsteroidGraphics(asteroid) {
    const g = new PIXI.Graphics();
    
    // Draw asteroid polygon in white
    g.lineStyle(2, 0xffffff);
    if (asteroid.Polygon && asteroid.Polygon.length === 24) {
        g.moveTo(asteroid.Polygon[0], asteroid.Polygon[1]);
        for (let i = 2; i < 24; i += 2) {
            g.lineTo(asteroid.Polygon[i], asteroid.Polygon[i + 1]);
        }
        g.closePath();
    }
    
    app.stage.addChild(g);
    return g;
}

// Update asteroid graphics
function updateAsteroidGraphics(asteroid) {
    let g = asteroidGraphics.get(asteroid.Id);
    let cache = graphicsCache.get(asteroid.Id);
    
    // Create new graphics if needed
    if (!g) {
        g = createAsteroidGraphics(asteroid);
        asteroidGraphics.set(asteroid.Id, g);
        cache = {};
        graphicsCache.set(asteroid.Id, cache);
    }
    
    // Check if asteroid data has changed
    if (cache.size !== asteroid.Size) {
        g.clear();

        g.lineStyle(2, 0xffffff);
        if (asteroid.Polygon && asteroid.Polygon.length === 24) {
            g.moveTo(asteroid.Polygon[0], asteroid.Polygon[1]);
            for (let i = 2; i < 24; i += 2) {
                g.lineTo(asteroid.Polygon[i], asteroid.Polygon[i + 1]);
            }
            g.closePath();
        }
        
        cache.size = asteroid.Size;
    }
    
    // Always update position
    g.x = asteroid.Position.X - camera.x + app.renderer.width / 2;
    g.y = asteroid.Position.Y - camera.y + app.renderer.height / 2;

    return g;
}

// Track which asteroids need cleanup
let pendingCleanup = false;
let lastAsteroidIds = new Set();

// WebSocket message handler - just updates asteroid data
ws.onmessage = (msg) => {
    try {
        // Quick parse of the data
        asteroids = JSON.parse(msg.data);
        
        // Mark that we need to do cleanup on next frame
        pendingCleanup = true;
    } catch (e) {
        console.error("Invalid asteroid data", e);
    }
};

// Add render loop
app.ticker.add(() => {
    // Handle asteroid cleanup if needed
    if (pendingCleanup) {
        const currentIds = new Set(asteroids.map(a => a.Id));
        
        // Remove graphics for asteroids that no longer exist
        for (const id of lastAsteroidIds) {
            if (!currentIds.has(id) && asteroidGraphics.has(id)) {
                const graphics = asteroidGraphics.get(id);
                app.stage.removeChild(graphics);
                asteroidGraphics.delete(id);
                graphicsCache.delete(id);
            }
        }
        
        lastAsteroidIds = currentIds;
        pendingCleanup = false;
    }
    
    // Only update asteroids in view
    const margin = 100; // pixels
    const minX = -margin;
    const minY = -margin;
    const maxX = app.renderer.width + margin;
    const maxY = app.renderer.height + margin;
    
    asteroids.forEach(asteroid => {
        const screenX = asteroid.Position.X - camera.x + app.renderer.width / 2;
        const screenY = asteroid.Position.Y - camera.y + app.renderer.height / 2;
        
        // Only update if asteroid is on screen
        if (screenX > minX && screenX < maxX && screenY > minY && screenY < maxY) {
            updateAsteroidGraphics(asteroid);
        } else if (asteroidGraphics.has(asteroid.Id)) {
            // Hide off-screen asteroids
            const g = asteroidGraphics.get(asteroid.Id);
            g.visible = false;
        }
    });
});
ws.onclose = () => {
    console.log("WebSocket closed");
};
