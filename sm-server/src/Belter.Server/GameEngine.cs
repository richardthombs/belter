using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;

public class GameWorld
{
	public const long WORLD_WIDTH = 1 << 20;
	public const long WORLD_HEIGHT = 1 << 20;
	public List<GameObject> objects = [];
	public Dictionary<string, GameObject> players = [];
	ulong nextEntityId = 1;

	public readonly Rectangle WorldRectangle = new Rectangle(-WORLD_WIDTH / 2, -WORLD_HEIGHT / 2, WORLD_WIDTH, WORLD_HEIGHT);

	public void AddObject(GameObject obj)
	{
		obj.Id = nextEntityId++;
		objects.Add(obj);
	}

	public void AddPlayer(string playerName, GameObject obj)
	{
		obj.Id = nextEntityId++;
		if (!players.ContainsKey(playerName)) players.Add(playerName, obj);
	}

	public void SpawnPlayer(string playerName)
	{
		var player = new GameObject
		{
			X = 0, //rnd.NextInt64((long)WORLD_WIDTH),
			Y = 0, //rnd.NextInt64((long)WORLD_HEIGHT),
			R = 180, //rnd.Next(360),
			dX = 0, //rnd.Next(-5, 5) * 20,
			dY = 0, //rnd.Next(-5, 5) * 20,
			dR = 0, //(rnd.NextDouble() * 90 - 45) * 3,
			Type = "p"
		};
		AddPlayer(playerName, player);
	}
}

public class GameEngine : BackgroundService
{
	public GameWorld world;
	IHubContext<GameHub> hub;
	ILogger<GameEngine> logger;
	Random rnd;

	public GameEngine(IHubContext<GameHub> hub, ILogger<GameEngine> logger, GameWorld world)
	{
		this.hub = hub;
		this.logger = logger;
		this.world = world;

		this.rnd = new();

		for (int i = 0; i < 1e5; i++)
		{
			var asteroid = new GameObject
			{
				X = rnd.NextInt64(-GameWorld.WORLD_WIDTH / 2, GameWorld.WORLD_WIDTH / 2),
				Y = rnd.NextInt64(-GameWorld.WORLD_HEIGHT / 2, GameWorld.WORLD_HEIGHT / 2),
				R = rnd.Next(360),
				dX = RandomInt(-20, 20, true) * 5,
				dY = RandomInt(-20, 20, true) * 5,
				dR = rnd.NextDouble() * 20 * (rnd.Next(2) == 1 ? 1 : -1),
				Type = "a",
				Radius = (ulong)rnd.Next(5, 50) * 5
			};
			world.AddObject(asteroid);
		}

		for (int i = 0; i < 1; i++)
		{
			var player = new GameObject
			{
				X = 0, //rnd.NextInt64((long)WORLD_WIDTH),
				Y = 0, //rnd.NextInt64((long)WORLD_HEIGHT),
				R = 180, //rnd.Next(360),
				dX = 0, //rnd.Next(-5, 5) * 20,
				dY = 0, //rnd.Next(-5, 5) * 20,
				dR = 0, //(rnd.NextDouble() * 90 - 45) * 3,
				Type = "p"
			};
			world.AddObject(player);
		}
	}

	public void SpawnPlayer(string playerName)
	{
		world.AddPlayer(playerName, new GameObject
		{
			X = rnd.NextInt64(GameWorld.WORLD_WIDTH),
			Y = rnd.NextInt64(GameWorld.WORLD_HEIGHT),
			R = rnd.Next(360),
			dX = rnd.Next(-5, 5) * 20,
			dY = rnd.Next(-5, 5) * 20,
			dR = (rnd.NextDouble() * 90 - 45) * 3,
			Type = "p"
		});
	}

	protected override async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		var framesPerSecond = 30d;
		double secondsPerFrame = 1 / framesPerSecond;
		double msPerFrame = (long)(1000 * secondsPerFrame);
		var watch = new Stopwatch();
		var fps = new MovingAverage(60);
		var frame = 0;

		logger.LogInformation("Game engine startup");
		logger.LogInformation("Target FPS = {targetFps}, ms per frame = {msPerFrame}, scale = {scale}", framesPerSecond, msPerFrame, secondsPerFrame);

		while (!cancellationToken.IsCancellationRequested)
		{
			watch.Restart();

			var tree = new QuadTreeNode { Bounds = world.WorldRectangle, Capacity = 100000 };

			foreach (var obj in world.objects)
			{
				obj.X += (long)(obj.dX * secondsPerFrame);
				obj.Y += (long)(obj.dY * secondsPerFrame);
				obj.R = Clamp(obj.R + obj.dR * secondsPerFrame, 0, 360);
				tree.Add(obj);
			}

			foreach (var player in world.players)
			{
				var playerObj = player.Value;

				playerObj.X = Clamp((long)(playerObj.X + playerObj.dX * secondsPerFrame), long.MinValue, long.MaxValue);
				playerObj.Y = Clamp((long)(playerObj.Y + playerObj.dY * secondsPerFrame), long.MinValue, long.MaxValue);
				playerObj.R = Clamp(playerObj.R + playerObj.dR * secondsPerFrame, 0, 360);
				tree.Add(playerObj);
			}

			foreach (var player in world.players)
			{
				var user = hub.Clients.User(player.Key);

				var visibleRect = new Rectangle(player.Value.X - 1000, player.Value.Y - 1000, 2000, 2000);
				var updates = tree.FindWithin(visibleRect);

				await user.SendAsync("PositionUpdate", updates);
			}

			var msUpdate = watch.ElapsedMilliseconds;

			var free = msPerFrame - msUpdate;
			var delay = free <= 0? 1 : (int)free;
			await Task.Delay(delay, cancellationToken);

			fps.ComputeAverage(1000 / watch.ElapsedMilliseconds);

			if (frame % 1000 == 0) Console.WriteLine($"{fps.Average:n0}fps : {msUpdate:n0}ms to update, {free:n0}ms free");
			frame++;
		}
	}

	long Clamp(long val, long min, long max)
	{
		if (val < min) return max;
		if (val > max) return min;
		return val;
	}

	double Clamp(double val, double min, double max)
	{
		if (val < min) return max;
		if (val > max) return min;
		return val;
	}

	double Random(double min, double max)
	{
		return rnd.NextDouble() * (max - min) + min;
	}

	int RandomInt(int min, int max, bool noZeros)
	{
		var result = rnd.Next(min, max);
		if (noZeros && result == 0) return RandomInt(min, max, noZeros);
		return result;
	}
}
