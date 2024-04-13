namespace Belter.GameServer;

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