using System.Numerics;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Net;
using System.Threading;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Configuration
const float WORLD_WIDTH = 1_000_000f;
const float WORLD_HEIGHT = 1_000_000f;
const int ASTEROID_COUNT = 200;
const int ASTEROID_POINTS = 12;
const float ASTEROID_MIN_SIZE = 30f;
const float ASTEROID_MAX_SIZE = 80f;
const float ASTEROID_SPEED = 2.5f;


// Asteroid Generation
List<Asteroid> GenerateAsteroids(int count)
{
    var rng = new Random();
    var asteroids = new List<Asteroid>();
    for (int i = 0; i < count; i++)
    {
        float size = rng.NextSingle() * (ASTEROID_MAX_SIZE - ASTEROID_MIN_SIZE) + ASTEROID_MIN_SIZE;
        float[] polygon = new float[ASTEROID_POINTS * 2];
        double angleStep = 2 * Math.PI / ASTEROID_POINTS;
        for (int j = 0; j < ASTEROID_POINTS; j++)
        {
            double angle = j * angleStep;
            float radius = size * (0.7f + 0.6f * rng.NextSingle());
            polygon[j * 2] = radius * (float)Math.Cos(angle);
            polygon[j * 2 + 1] = radius * (float)Math.Sin(angle);
        }
        // Place asteroid within a radius of 5000 from the map center
        float centerX = WORLD_WIDTH / 2f;
        float centerY = WORLD_HEIGHT / 2f;
        double theta = rng.NextDouble() * 2 * Math.PI;
        double spawnRadius = 5000 * Math.Sqrt(rng.NextDouble()); // sqrt for uniform distribution
        float x = centerX + (float)(spawnRadius * Math.Cos(theta));
        float y = centerY + (float)(spawnRadius * Math.Sin(theta));
        var pos = new Vector2(x, y);
        var vel = new Vector2((rng.NextSingle() - 0.5f) * ASTEROID_SPEED, (rng.NextSingle() - 0.5f) * ASTEROID_SPEED);
        asteroids.Add(new Asteroid(i, pos, vel, size, polygon));
    }
    return asteroids;
}
var asteroids = GenerateAsteroids(ASTEROID_COUNT);


var sockets = new ConcurrentBag<WebSocket>();

// WebSocket Endpoint
app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }
    var ws = await context.WebSockets.AcceptWebSocketAsync();
    sockets.Add(ws);
    var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var remotePort = context.Connection.RemotePort;
    Console.WriteLine($"[WS] Client connected: {remoteIp}:{remotePort}");
    var buffer = new byte[1024];
    while (ws.State == WebSocketState.Open)
    {
        var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        if (result.MessageType == WebSocketMessageType.Close)
        {
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            Console.WriteLine($"[WS] Client disconnected: {remoteIp}:{remotePort}");
        }
        else if (result.MessageType == WebSocketMessageType.Text)
        {
            string msg = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(msg);
                if (doc.RootElement.TryGetProperty("type", out var typeElem) && typeElem.GetString() == "keys")
                {
                    if (doc.RootElement.TryGetProperty("keys", out var keysElem) && keysElem.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        var keys = keysElem.EnumerateArray().Select(k => k.GetString()).Where(s => s != null).ToArray();
                        Console.WriteLine($"[WS] Keys from {remoteIp}:{remotePort}: [{string.Join(", ", keys)}]");
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

// Asteroid Update & Broadcast Loop
var timer = new Timer(async _ =>
{
    float centerX = WORLD_WIDTH / 2f;
    float centerY = WORLD_HEIGHT / 2f;
    double maxDistance = 10000.0;
    for (int i = 0; i < asteroids.Count; i++)
    {
        var a = asteroids[i];
        a.Position += a.Velocity;
        // Wrap around game world
        if (a.Position.X < 0) a.Position = new Vector2(WORLD_WIDTH, a.Position.Y);
        if (a.Position.X > WORLD_WIDTH) a.Position = new Vector2(0, a.Position.Y);
        if (a.Position.Y < 0) a.Position = new Vector2(a.Position.X, WORLD_HEIGHT);
        if (a.Position.Y > WORLD_HEIGHT) a.Position = new Vector2(a.Position.X, 0);
        // Check if asteroid is too far from center
        double dx = a.Position.X - centerX;
        double dy = a.Position.Y - centerY;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist > maxDistance)
        {
            // Replace asteroid with a new one (new ID)
            int newId = asteroids.Max(ast => ast.Id) + 1;
            asteroids[i] = GenerateAsteroids(1).First();
            asteroids[i].Id = newId;
        }
    }
    var asteroidDtos = asteroids.Select(a => new {
        a.Id,
        Position = new { X = a.Position.X, Y = a.Position.Y },
        Velocity = new { X = a.Velocity.X, Y = a.Velocity.Y },
        a.Size,
        a.Polygon
    });
    var msg = JsonSerializer.Serialize(asteroidDtos);
    var msgBytes = System.Text.Encoding.UTF8.GetBytes(msg);
    foreach (var ws in sockets)
    {
        if (ws.State == WebSocketState.Open)
        {
            try { await ws.SendAsync(new ArraySegment<byte>(msgBytes), WebSocketMessageType.Text, true, CancellationToken.None); } catch { }
        }
    }
}, null, 0, 33); // ~30 FPS


app.MapGet("/", () => "Hello World!");
app.Run();
