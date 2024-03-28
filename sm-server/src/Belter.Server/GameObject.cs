public class GameObject : IPoint
{
	public ulong Id { get; set; }
	public long X { get; set; }
	public long Y { get; set; }
	public double R { get; set; }
	public Int32 dX { get; set; }
	public Int32 dY { get; set; }
	public double dR { get; set; }
	public string Type { get; set; } = "?";
	public ulong Radius { get; set; }
}

public interface IPoint
{
	long X { get; }
	long Y { get; }
}