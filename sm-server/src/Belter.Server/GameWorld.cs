using System.Security.Cryptography.X509Certificates;

public class GameWorld
{
	public const long WORLD_WIDTH = 1 << 20;
	public const long WORLD_HEIGHT = 1 << 20;
	public List<GameObject> objects = [];
	public Dictionary<string, GameObject> players = [];
	public Dictionary<string, Subscription> subs = [];
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

	public void SetSubscription(string playerName, Subscription sub)
	{
		subs[playerName] = sub;
	}
}
