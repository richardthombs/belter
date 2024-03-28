using Microsoft.AspNetCore.SignalR;

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
		world.SpawnPlayer(Context.UserIdentifier ?? "Foobar");
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

		foreach (var u in users) Console.WriteLine(u);

		var playerIndex = users.IndexOf(Context.UserIdentifier);
		Clients.User(Context.UserIdentifier).SendAsync("Welcome", playerIndex);

		logger.LogInformation("Player index: {playerIndex}", playerIndex);
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