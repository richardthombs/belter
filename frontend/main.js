import * as PIXI from "pixi.js";

// Create PixiJS Application
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
let ships = [];
let myShipId = null;
let asteroidGraphics = new Map();
let shipGraphics = new Map();

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

// Cache for graphics data
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

// Create initial graphics for a ship
function createShipGraphics(ship) {
    const g = new PIXI.Graphics();

    // Draw ship polygon in blue for other players, green for local player
    g.lineStyle(2, 0xffffff)
        .beginFill(ship.Id === myShipId ? 0x00ff00 : 0x0000ff, 0.5)
        .moveTo(ship.Polygon[0], ship.Polygon[1])
        .lineTo(ship.Polygon[2], ship.Polygon[3])
        .lineTo(ship.Polygon[4], ship.Polygon[5])
        .lineTo(ship.Polygon[6], ship.Polygon[7])
        .lineTo(ship.Polygon[8], ship.Polygon[9])
        .closePath()
        .endFill();

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

    // Update position and set visibility
    const screenX = asteroid.Position.X - camera.x + app.renderer.width / 2;
    const screenY = asteroid.Position.Y - camera.y + app.renderer.height / 2;
    g.x = screenX;
    g.y = screenY;
    g.visible = true;

    return g;
}

// Update ship graphics
function updateShipGraphics(ship) {
    let g = shipGraphics.get(ship.Id);

    // Create new graphics if needed
    if (!g) {
        g = createShipGraphics(ship);
        shipGraphics.set(ship.Id, g);
    }

    // Always update position and rotation
    const screenX = ship.Position.X - camera.x + app.renderer.width / 2;
    const screenY = ship.Position.Y - camera.y + app.renderer.height / 2;
    g.x = screenX;
    g.y = screenY;
    g.rotation = ship.Rotation;
    g.visible = true;

    return g;
}

// Track which objects need cleanup
let pendingCleanup = false;
let lastAsteroidIds = new Set();
let lastShipIds = new Set();

// WebSocket message handler
ws.onmessage = (msg) => {
    try {
        const gameState = JSON.parse(msg.data);
        asteroids = gameState.Asteroids;
        ships = gameState.Ships;

        // If this is our first update, find our ship
        if (!myShipId && ships.length > 0) {
            // The first ship we see is ours (we'll receive the update right after connecting)
            myShipId = ships[0].Id;
            console.log("My ship ID:", myShipId);
        }

        // Mark that we need to do cleanup on next frame
        pendingCleanup = true;
    } catch (e) {
        console.error("Invalid game state data", e);
    }
};

// Add render loop
app.ticker.add(() => {
    // Handle cleanup if needed
    if (pendingCleanup) {
        const currentAsteroidIds = new Set(asteroids.map(a => a.Id));
        const currentShipIds = new Set(ships.map(s => s.Id));

        // Remove graphics for objects that no longer exist
        for (const id of lastAsteroidIds) {
            if (!currentAsteroidIds.has(id) && asteroidGraphics.has(id)) {
                const graphics = asteroidGraphics.get(id);
                app.stage.removeChild(graphics);
                asteroidGraphics.delete(id);
                graphicsCache.delete(id);
            }
        }

        for (const id of lastShipIds) {
            if (!currentShipIds.has(id) && shipGraphics.has(id)) {
                const graphics = shipGraphics.get(id);
                app.stage.removeChild(graphics);
                shipGraphics.delete(id);
            }
        }

        lastAsteroidIds = currentAsteroidIds;
        lastShipIds = currentShipIds;
        pendingCleanup = false;
    }

    // Update camera to follow player's ship
    if (myShipId) {
        const myShip = ships.find(s => s.Id === myShipId);
        if (myShip) {
            camera.x = myShip.Position.X;
            camera.y = myShip.Position.Y;
        }
    }

    // Only update objects in view
    const margin = 100; // pixels
    const minX = -margin;
    const minY = -margin;
    const maxX = app.renderer.width + margin;
    const maxY = app.renderer.height + margin;

    // Update asteroids
    asteroids.forEach(asteroid => {
        const screenX = asteroid.Position.X - camera.x + app.renderer.width / 2;
        const screenY = asteroid.Position.Y - camera.y + app.renderer.height / 2;

        if (screenX > minX && screenX < maxX && screenY > minY && screenY < maxY) {
            updateAsteroidGraphics(asteroid);
        } else if (asteroidGraphics.has(asteroid.Id)) {
            const g = asteroidGraphics.get(asteroid.Id);
            g.visible = false;
        }
    });

    // Update ships
    ships.forEach(ship => {
        const screenX = ship.Position.X - camera.x + app.renderer.width / 2;
        const screenY = ship.Position.Y - camera.y + app.renderer.height / 2;

        if (screenX > minX && screenX < maxX && screenY > minY && screenY < maxY) {
            updateShipGraphics(ship);
        } else if (shipGraphics.has(ship.Id)) {
            const g = shipGraphics.get(ship.Id);
            g.visible = false;
        }
    });
});

ws.onclose = () => {
    console.log("WebSocket closed");
};
