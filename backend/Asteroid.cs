using System.Numerics;

public class Asteroid
{
    public int Id { get; set; }
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public float[] Polygon { get; set; } // 24 floats: x0, y0, x1, y1, ...
    public float Size { get; set; }

    public Asteroid(int id, Vector2 position, Vector2 velocity, float size, float[] polygon)
    {
        Id = id;
        Position = position;
        Velocity = velocity;
        Size = size;
        Polygon = polygon;
    }
}
