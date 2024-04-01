using Microsoft.AspNetCore.SignalR;

namespace Belter.Server;

public class GameHub : Hub
{
	ILogger<GameHub> logger;
	GameWorld world;
	static List<string> users = new();


	public GameHub(ILogger<GameHub> logger, GameWorld world)
	{
		this.logger = logger;
		this.world = world;
	}

	public override Task OnConnectedAsync()
	{
		logger.LogInformation($"Connected: {Context.UserIdentifier} / {Context.ConnectionId}");
		world.SpawnPlayer(Context.UserIdentifier ?? throw new ArgumentNullException());
		return base.OnConnectedAsync();
	}

	public override Task OnDisconnectedAsync(Exception? exception)
	{
		logger.LogInformation($"Disconnected: {Context.UserIdentifier} / {Context.ConnectionId} - {exception?.Message ?? "Uknown"}");

		return base.OnDisconnectedAsync(exception);
	}

	public void ClientConnected(ConnectionMessage connect)
	{
		logger.LogInformation($"Game client startup: User: {Context.UserIdentifier} / {Context.ConnectionId}, message: {connect.Message}");

		if (String.IsNullOrEmpty(Context.UserIdentifier)) return;

		if (!users.Contains(Context.UserIdentifier)) users.Add(Context.UserIdentifier);

		var userList = new List<string>();
		for (int i = 0; i < users.Count; i++)
		{
			userList.Add($"{i}: {users[i]}");
		}
		logger.LogInformation("User list\n{userList}", String.Join("\n", userList));

		var playerIndex = users.IndexOf(Context.UserIdentifier);
		Clients.User(Context.UserIdentifier).SendAsync("Welcome", new { PlayerIndex = playerIndex, Context.ConnectionId, Context.UserIdentifier });
	}

	public void Subscribe(Subscription sub)
	{
		world.SetSubscription(Context.UserIdentifier ?? throw new ArgumentNullException(), sub);
		logger.LogInformation("{user} subscription changed to {sub}", Context.UserIdentifier, sub);
	}

	public void KeyState(KeyState keyState)
	{
		if (keyState.Thrust) logger.LogInformation($"{Context.UserIdentifier} thrusting");
		if (keyState.RotLeft) logger.LogInformation($"{Context.UserIdentifier} rotating left");
		if (keyState.RotRight) logger.LogInformation($"{Context.UserIdentifier} rotating right");
		if (keyState.Fire) logger.LogInformation($"{Context.UserIdentifier} firing");
	}
}

public record KeyState
{
	public bool Thrust { get; set; }
	public bool RotLeft { get; set; }
	public bool RotRight { get; set; }
	public bool Fire { get; set; }
}

public record Subscription
{
	public long X { get; set; }
	public long Y { get; set; }
	public ulong W { get; set; }
	public ulong H { get; set; }
	public double Z { get; set; }

	public override string ToString()
	{
		return $"({X},{Y}) + ({W},{H})";
	}
}