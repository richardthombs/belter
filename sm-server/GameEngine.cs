using System.Diagnostics;
using Microsoft.AspNetCore.SignalR;

public class GameEngine : BackgroundService
{
	const long WORLD_WIDTH = 30000;
	const long WORLD_HEIGHT = 30000;

	IHubContext<GameHub> hub;
	ILogger<GameEngine> logger;

	List<GameObject> objects;
	Dictionary<string, GameObject> players;
	ulong nextEntityId = 1;
	ulong frame = 1;
	Random rnd;

	public GameEngine(IHubContext<GameHub> hub, ILogger<GameEngine> logger)
	{
		this.hub = hub;
		this.logger = logger;
		this.objects = new();
		this.players = new();
		this.rnd = new();

		for (int i = 0; i < 1000; i++)
		{
			var asteroid = new GameObject
			{
				Id = nextEntityId++,
				X = rnd.NextInt64(WORLD_WIDTH) * (rnd.Next(2) == 1 ? 1 : -1),
				Y = rnd.NextInt64(WORLD_HEIGHT) * (rnd.Next(2) == 1 ? 1 : -1),
				R = rnd.Next(360),
				dX = RandomInt(-20, 20, true) * 5,
				dY = RandomInt(-20, 20, true) * 5,
				dR = rnd.NextDouble() * 20 * (rnd.Next(2) == 1 ? 1 : -1),
				Type = "a",
				Radius = (ulong)rnd.Next(5, 50) * 5
			};
			objects.Add(asteroid);
		}

		for (int i = 0; i < 1; i++)
		{
			var player = new GameObject
			{
				Id = nextEntityId++,
				X = 0, //rnd.NextInt64((long)WORLD_WIDTH),
				Y = 0, //rnd.NextInt64((long)WORLD_HEIGHT),
				R = 180, //rnd.Next(360),
				dX = 0, //rnd.Next(-5, 5) * 20,
				dY = 0, //rnd.Next(-5, 5) * 20,
				dR = 0, //(rnd.NextDouble() * 90 - 45) * 3,
				Type = "p"
			};
			objects.Add(player);
		}
	}

	void SpawnPlayer(string playerName)
	{
		players.Add(playerName, new GameObject
		{
			Id = nextEntityId++,
			X = rnd.NextInt64(WORLD_WIDTH),
			Y = rnd.NextInt64(WORLD_HEIGHT),
			R = rnd.Next(360),
			dX = rnd.Next(-5, 5) * 20,
			dY = rnd.Next(-5, 5) * 20,
			dR = (rnd.NextDouble() * 90 - 45) * 3,
			Type = "p"
		});
	}

	protected override async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		/*		var task = Task.Run(async () =>
				{
					var sw = new Stopwatch();
					sw.Start();

					while (!cancellationToken.IsCancellationRequested)
					{
						await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
						logger.LogInformation($"Server FPS: {frame / sw.Elapsed.TotalSeconds:n0}");
					}
				});
		*/
		var framesPerSecond = 30d;
		double secondsPerFrame = 1 / framesPerSecond;
		double msPerFrame = (long)(1000 * secondsPerFrame);
		var watch = new Stopwatch();

		logger.LogInformation("Game engine startup. Updating every {msPerFrame}ms, scale = {scale}", msPerFrame, secondsPerFrame);

		while (!cancellationToken.IsCancellationRequested)
		{
			var updates = new List<GameObject>();
			watch.Restart();

			foreach (var obj in objects)
			{
				obj.X += (long)(obj.dX * secondsPerFrame);
				obj.Y += (long)(obj.dY * secondsPerFrame);
				obj.R = Clamp(obj.R + obj.dR * secondsPerFrame, 0, 360);
				updates.Add(obj);
			}

			foreach (var player in players.Values)
			{
				player.X = Clamp((long)(player.X + player.dX * secondsPerFrame), long.MinValue, long.MaxValue);
				player.Y = Clamp((long)(player.Y + player.dY * secondsPerFrame), long.MinValue, long.MaxValue);
				player.R = Clamp(player.R + player.dR * secondsPerFrame, 0, 360);
				updates.Add(player);
			}

			await hub.Clients.All.SendAsync("PositionUpdate", updates);
			var delay = msPerFrame - watch.ElapsedMilliseconds;
			if (delay > 0) await Task.Delay((int)delay, cancellationToken);

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