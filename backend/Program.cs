using System.Numerics;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Net;
using System.Threading;

public class Program
{
    // Configuration
    public const float WORLD_WIDTH = 1_000_000f;
    public const float WORLD_HEIGHT = 1_000_000f;
    private const int ASTEROID_COUNT = 200;
    private const int ASTEROID_POINTS = 12;
    private const float ASTEROID_MIN_SIZE = 30f;
    private const float ASTEROID_MAX_SIZE = 80f;
    private const float ASTEROID_SPEED = 2.5f;
    private const float ASTEROID_SPAWN_RADIUS = 2000f;

    // Generate polygon points for an asteroid
    public static float[] GenerateAsteroidPolygon(float size, Random? rng = null)
    {
        rng ??= new Random();
        float[] polygon = new float[ASTEROID_POINTS * 2];
        double angleStep = 2 * Math.PI / ASTEROID_POINTS;

        for (int j = 0; j < ASTEROID_POINTS; j++)
        {
            double angle = j * angleStep;
            // Vary radius between 60% and 100% of size/2 (the asteroid's radius)
            float radius = (size / 2) * (0.6f + 0.4f * rng.NextSingle());
            polygon[j * 2] = radius * (float)Math.Cos(angle);
            polygon[j * 2 + 1] = radius * (float)Math.Sin(angle);
        }

        return polygon;
    }

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();

        // Asteroid Generation
        List<Asteroid> GenerateAsteroids(int count)
        {
            var rng = new Random();
            var asteroids = new List<Asteroid>();
            for (int i = 0; i < count; i++)
            {
                float size = rng.NextSingle() * (ASTEROID_MAX_SIZE - ASTEROID_MIN_SIZE) + ASTEROID_MIN_SIZE;
                float[] polygon = GenerateAsteroidPolygon(size, rng);
                // Place asteroid within spawn radius from the map center
                float centerX = WORLD_WIDTH / 2f;
                float centerY = WORLD_HEIGHT / 2f;
                double theta = rng.NextDouble() * 2 * Math.PI;
                double spawnRadius = ASTEROID_SPAWN_RADIUS * Math.Sqrt(rng.NextDouble()); // sqrt for uniform distribution
                float x = centerX + (float)(spawnRadius * Math.Cos(theta));
                float y = centerY + (float)(spawnRadius * Math.Sin(theta));
                var pos = new Vector2(x, y);
                var vel = new Vector2((rng.NextSingle() - 0.5f) * ASTEROID_SPEED, (rng.NextSingle() - 0.5f) * ASTEROID_SPEED);
                // Vary density between 0.5 and 2.0
                float density = 0.5f + 1.5f * rng.NextSingle();
                asteroids.Add(new Asteroid(i, pos, vel, size, polygon, density));
            }
            return asteroids;
        }
        var asteroids = GenerateAsteroids(ASTEROID_COUNT);
        Console.WriteLine($"[Asteroids] Created initial {asteroids.Count} asteroids");

        // Ship management
        var ships = new ConcurrentDictionary<string, Ship>();
        var socketToShipId = new ConcurrentDictionary<WebSocket, string>();
        var playerInputs = new ConcurrentDictionary<string, HashSet<string>>();
        var rng = new Random();

        // WebSocket Endpoint
        app.Map("/ws", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            var ws = await context.WebSockets.AcceptWebSocketAsync();
            var shipId = Guid.NewGuid().ToString();
            var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var remotePort = context.Connection.RemotePort;

            // Create ship at random position near the center
            double theta = rng.NextDouble() * 2 * Math.PI;
            double spawnRadius = ASTEROID_SPAWN_RADIUS * Math.Sqrt(rng.NextDouble());
            float x = WORLD_WIDTH / 2 + (float)(spawnRadius * Math.Cos(theta));
            float y = WORLD_HEIGHT / 2 + (float)(spawnRadius * Math.Sin(theta));
            var ship = new Ship(shipId, new Vector2(x, y));

            ships.TryAdd(shipId, ship);
            socketToShipId.TryAdd(ws, shipId);
            playerInputs.TryAdd(shipId, new HashSet<string>());

            Console.WriteLine($"[WS] Client connected: {remoteIp}:{remotePort} (Ship: {shipId})");

            var buffer = new byte[1024];
            while (ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    Console.WriteLine($"[WS] Client disconnected: {remoteIp}:{remotePort} (Ship: {shipId})");
                    ships.TryRemove(shipId, out _);
                    socketToShipId.TryRemove(ws, out _);
                    playerInputs.TryRemove(shipId, out _);
                }
                else if (result.MessageType == WebSocketMessageType.Text)
                {
                    string msg = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                    try
                    {
                        var doc = System.Text.Json.JsonDocument.Parse(msg);
                        if (doc.RootElement.TryGetProperty("type", out var typeElem) && typeElem.GetString() == "keys")
                        {
                            if (doc.RootElement.TryGetProperty("keys", out var keysElem) && keysElem.ValueKind == JsonValueKind.Array)
                            {
                                var keys = keysElem.EnumerateArray()
                                    .Select(k => k.GetString())
                                    .Where(s => s != null)
                                    .ToHashSet();
                                playerInputs[shipId] = keys;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[WS] Invalid client message: {msg} ({ex.Message})");
                    }
                }
            }
        });

        app.UseWebSockets();

        // Game Update & Broadcast Loop
        var updateLock = new object();
        var updateInProgress = false;
        var timer = new Timer(async _ =>
        {
            if (Interlocked.CompareExchange(ref updateInProgress, true, false))
            {
                Console.WriteLine("[Warning] Update loop taking longer than 33ms, skipping update");
                return;
            }

            try
            {
                float centerX = WORLD_WIDTH / 2f;
                float centerY = WORLD_HEIGHT / 2f;
                double maxDistance = ASTEROID_SPAWN_RADIUS * 2.0;

                // Update ships
                foreach (var (shipId, ship) in ships)
                {
                    if (playerInputs.TryGetValue(shipId, out var keys))
                    {
                        ship.Update(keys);
                    }
                }

                lock (updateLock)
                {
                    // Update asteroids positions and handle collisions
                    for (int i = 0; i < asteroids.Count; i++)
                    {
                        var a = asteroids[i];
                        a.Position += a.Velocity;
                        // Wrap around game world
                        if (a.Position.X < 0) a.Position = new Vector2(WORLD_WIDTH, a.Position.Y);
                        if (a.Position.X > WORLD_WIDTH) a.Position = new Vector2(0, a.Position.Y);
                        if (a.Position.Y < 0) a.Position = new Vector2(a.Position.X, WORLD_HEIGHT);
                        if (a.Position.Y > WORLD_HEIGHT) a.Position = new Vector2(a.Position.X, 0);
                    }

                    // Check collisions
                    for (int i = 0; i < asteroids.Count; i++)
                    {
                        var a = asteroids[i];
                        if (a.Size <= 0) continue; // Skip destroyed asteroids

                        // Check if asteroid is too far from center
                        double dx = a.Position.X - centerX;
                        double dy = a.Position.Y - centerY;
                        double dist = Math.Sqrt(dx * dx + dy * dy);
                        if (dist > maxDistance)
                        {
                            // Replace asteroid with a new one (new ID)
                            Console.WriteLine($"[Asteroids] Asteroid {asteroids[i].Id} destroyed (out of bounds). Total: {asteroids.Count}");
                            int newId = asteroids.Max(ast => ast.Id) + 1;
                            asteroids[i] = GenerateAsteroids(1).First();
                            asteroids[i].Id = newId;
                            Console.WriteLine($"[Asteroids] Created new asteroid {newId}. Total: {asteroids.Count}");
                            continue;
                        }

                        // Check collisions with other asteroids
                        for (int j = i + 1; j < asteroids.Count; j++)
                        {
                            var b = asteroids[j];
                            if (b.Size <= 0) continue; // Skip destroyed asteroids

                            if (a.IsCollidingWith(b))
                            {
                                a.HandleCollision(b);
                            }
                        }
                    }

                    // Remove destroyed asteroids and replace them with new ones
                    for (int i = 0; i < asteroids.Count; i++)
                    {
                        if (asteroids[i].Size <= 0)
                        {
                            Console.WriteLine($"[Asteroids] Asteroid {asteroids[i].Id} destroyed (size <= 0). Total: {asteroids.Count}");
                            int newId = asteroids.Max(ast => ast.Id) + 1;
                            asteroids[i] = GenerateAsteroids(1).First();
                            asteroids[i].Id = newId;
                            Console.WriteLine($"[Asteroids] Created new asteroid {newId}. Total: {asteroids.Count}");
                        }
                    }
                }

                // Prepare game state update
                var gameState = new
                {
                    Ships = ships.Values.Select(s => new
                    {
                        s.Id,
                        Position = new { X = s.Position.X, Y = s.Position.Y },
                        Velocity = new { X = s.Velocity.X, Y = s.Velocity.Y },
                        s.Rotation,
                        s.Polygon
                    }),
                    Asteroids = asteroids.Select(a => new
                    {
                        a.Id,
                        Position = new { X = a.Position.X, Y = a.Position.Y },
                        Velocity = new { X = a.Velocity.X, Y = a.Velocity.Y },
                        a.Size,
                        a.Polygon
                    })
                };

                var msg = JsonSerializer.Serialize(gameState);
                var msgBytes = System.Text.Encoding.UTF8.GetBytes(msg);

                // Send updates to all connected clients
                var sendTasks = new List<Task>();
                foreach (var ws in socketToShipId.Keys)
                {
                    if (ws.State == WebSocketState.Open)
                    {
                        try
                        {
                            sendTasks.Add(ws.SendAsync(
                                new ArraySegment<byte>(msgBytes),
                                WebSocketMessageType.Text,
                                true,
                                CancellationToken.None));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[WS] Error sending update: {ex.Message}");
                        }
                    }
                }

                if (sendTasks.Any())
                {
                    await Task.WhenAll(sendTasks);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error] Update loop error: {ex.Message}");
            }
            finally
            {
                updateInProgress = false;
            }
        }, null, 0, 33); // ~30 FPS

        // Make sure timer is disposed when application stops
        app.Lifetime.ApplicationStopping.Register(() => timer?.Dispose());


        app.MapGet("/", () => "Hello World!");
        app.Run();
    }
}
