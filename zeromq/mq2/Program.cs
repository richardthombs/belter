using NetMQ;
using NetMQ.Sockets;

using (var sub = new SubscriberSocket())
{
	sub.Connect("tcp://localhost:5556");
	sub.SubscribeToAnyTopic();

	while (true)
	{
		var message = sub.ReceiveFrameString();
		Console.WriteLine(message);
	}
}