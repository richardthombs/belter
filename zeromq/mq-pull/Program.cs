using NetMQ;
using NetMQ.Sockets;

using (var pull = new PullSocket())
{
	pull.Connect("tcp://localhost:5678");
	Console.WriteLine("Connected");

	while (true)
	{
		var msg = pull.ReceiveFrameString();
		Console.WriteLine($"Pull received \"{msg}\" at {DateTime.Now}");
	}
}
