using NetMQ;
using NetMQ.Sockets;

using (var pub = new PublisherSocket())
{
	pub.Bind("tcp://localhost:5556");
	Thread.Sleep(500); // Needed to allow the socket to start up before sending first message

	int count = 0;
	while (true)
	{
		var msg = $"Hello {count++}";
		Console.WriteLine(msg);
		pub.SendFrame(msg);
		Thread.Sleep(1000);
	}
}
