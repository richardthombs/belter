using NetMQ;
using NetMQ.Sockets;

const string BEACON_PREFIX = "BELTER";
const int BEACON_PORT = 0x8000;

using (var beacon = new NetMQBeacon())
{
	beacon.Configure(BEACON_PORT);
	beacon.Publish($"{BEACON_PREFIX}:{Guid.NewGuid()}");
	beacon.Subscribe($"{BEACON_PREFIX}:");
	beacon.ReceiveReady += (o, e) =>
	{
		var message = e.Beacon.Receive();
		Console.WriteLine($"Beacon received from {message.PeerHost}: {message.String}");
	};

	var poller = new NetMQPoller { beacon };
	poller.Run();
}
