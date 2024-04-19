using NetMQ;
using NetMQ.Sockets;

using (var push = new PushSocket())
{
	push.Bind("tcp://localhost:5678");
	Console.WriteLine("Connected");

	while (true)
	{
		var msg = $"Hello, World! @ {DateTime.Now}";
		push.SendFrame(msg);
		Console.WriteLine($"Pushed \"{msg}\"");
		Thread.Sleep(5000);
	}
}
