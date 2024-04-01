using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;

namespace Belter.Server;

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
	}

	protected override async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		var targetFps = 10d;
		double msPerFrame = 1000 / targetFps;
		var watch = new Stopwatch();
		var fps = new MovingAverage(60);
		var frame = 0;

		logger.LogInformation("Game engine startup");
		logger.LogInformation("Target FPS = {targetFps} ({msPerFrame}ms per frame)", targetFps, msPerFrame);

		while (!cancellationToken.IsCancellationRequested)
		{
			var dt = frame == 0? 1 : (watch.ElapsedMilliseconds/1000f);
			watch.Restart();

			var tree = new QuadTreeNode { Bounds = world.WorldRectangle, Capacity = 100000 };

			foreach (var obj in world.objects)
			{
				obj.X += (long)(obj.dX * dt);
				obj.Y += (long)(obj.dY * dt);
				obj.R = Clamp(obj.R + obj.dR * dt, 0, 360);
				tree.Add(obj);
			}

			foreach (var player in world.players)
			{
				var playerObj = player.Value;

				playerObj.X = Clamp((long)(playerObj.X + playerObj.dX * dt), long.MinValue, long.MaxValue);
				playerObj.Y = Clamp((long)(playerObj.Y + playerObj.dY * dt), long.MinValue, long.MaxValue);
				playerObj.R = Clamp(playerObj.R + playerObj.dR * dt, 0, 360);
				tree.Add(playerObj);
			}

			foreach (var player in world.players)
			{
				var user = hub.Clients.User(player.Key);

				if (!world.subs.TryGetValue(player.Key, out var playerSub))
				{
					//logger.LogWarning("Can't find subscription for {player}", player.Key);
					continue;
				}

				var scale = 1/playerSub.Z;
				var visibleRect = new Rectangle(
					(long)(playerSub.X*scale),
					(long)(playerSub.Y*scale),
					(ulong)(playerSub.W*scale),
					(ulong)(playerSub.H*scale)
				);
				//logger.LogInformation("Visible rect {player}, {rect}", player.Key, visibleRect);
				var updates = tree.FindWithin(visibleRect);

				await user.SendAsync("PositionUpdate", updates);
			}

			var msUpdate = watch.ElapsedMilliseconds;

			var free = msPerFrame - msUpdate;
			var delay = free <= 0? 1 : (int)free;
			await Task.Delay(delay, cancellationToken);

			fps.ComputeAverage(1000m / watch.ElapsedMilliseconds);

			if (frame > 0 && frame % (targetFps * 60) == 0)
			{
				logger.LogTrace("{fpsAverage:n0}fps : {msUpdate:n0}ms to update, {free:n0}ms free", fps.Average, msUpdate, free);
			}
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
