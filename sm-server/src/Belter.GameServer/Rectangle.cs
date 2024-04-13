namespace Belter.GameServer;

public record Rectangle
{
	public Rectangle(long x, long y, ulong w, ulong h)
	{
		X = x;
		Y = y;
		Width = w;
		Height = h;
	}

	public long X { get; }
	public long Y { get; }
	public ulong Width { get; }
	public ulong Height { get; }

	public bool Contains(IPoint point)
	{
		if (point.X < X) return false;
		if (point.X - X > (long)Width) return false;
		if (point.Y < Y) return false;
		if (point.Y - Y > (long)Height) return false;

		return true;
	}

	public bool Intersects(Rectangle other)
	{
		var ax1 = this.X;
		var ay1 = this.Y;
		var ax2 = this.X + (long)(this.Width - 1);
		var ay2 = this.Y + (long)(this.Height - 1);

		var bx1 = other.X;
		var by1 = other.Y;
		var bx2 = other.X + (long)(other.Width - 1);
		var by2 = other.Y + (long)(other.Height - 1);

		return (bx2 >= ax1 && bx1 <= ax2) && (by2 >= ay1 && by1 <= ay2);
	}

	//
	// HTL  TR
	//
	// YBL  BR
	//  X    W

	public Rectangle TopLeftQuadrant => new Rectangle
	(
		X,
		Y + (long)(Height / 2),
		Width / 2,
		Height / 2
	);

	public Rectangle TopRightQuadrant => new Rectangle
	(
		X + (long)(Width / 2),
		Y + (long)(Height / 2),
		Width / 2,
		Height / 2
	);

	public Rectangle BottomLeftQuandrant => new Rectangle
	(
		X,
		Y,
		Width / 2,
		Height / 2
	);

	public Rectangle BottomRightQuadrant => new Rectangle
	(
		X + (long)(Width / 2),
		Y,
		Width / 2,
		Height / 2
	);

	public override string ToString()
	{
		return $"rect({X}, {Y}, {Width}, {Height})";
	}
}
