using System.Numerics;

public class Ship
{
    public string Id { get; private set; }  // Using connection ID as ship ID
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public float Rotation { get; set; }  // In radians
    public float[] Polygon { get; private set; }

    // Constants for ship properties
    private const float MAIN_ENGINE_THRUST = 0.15f;
    private const float RETRO_ENGINE_THRUST = 0.08f;
    private const float ROTATION_SPEED = 0.1f;
    private const float SHIP_SIZE = 30f;

    public Ship(string id, Vector2 position)
    {
        Id = id;
        Position = position;
        Velocity = Vector2.Zero;
        Rotation = 0;
        Polygon = GenerateShipPolygon();
    }

    private float[] GenerateShipPolygon()
    {
        // Convert the shape coordinates to match the provided design
        return new float[]
        {
            0, -20,    // Nose
            10, 10,    // Right corner
            0, 5,      // Right indent
            -10, 10,   // Left corner
            0, -20     // Back to nose
        };
    }

    public void Update(HashSet<string> keys)
    {
        // Rotation (A/D keys)
        if (keys.Contains("KeyA")) Rotation -= ROTATION_SPEED;
        if (keys.Contains("KeyD")) Rotation += ROTATION_SPEED;

        // Calculate direction vector based on rotation
        // Add -π/2 to rotation so 0 points up instead of right
        Vector2 direction = new Vector2(
            (float)Math.Cos(Rotation - Math.PI / 2),
            (float)Math.Sin(Rotation - Math.PI / 2)
        );

        // Main engine (W key) - Adds thrust in the direction the ship is facing
        if (keys.Contains("KeyW"))
        {
            Velocity += direction * MAIN_ENGINE_THRUST;
        }

        // Retro thrusters (S key) - Adds thrust opposite to the direction the ship is facing
        if (keys.Contains("KeyS"))
        {
            Velocity -= direction * RETRO_ENGINE_THRUST;
        }

        // Update position based on velocity
        Position += Velocity;

        // Wrap around game world
        if (Position.X < 0) Position = new Vector2(Program.WORLD_WIDTH, Position.Y);
        if (Position.X > Program.WORLD_WIDTH) Position = new Vector2(0, Position.Y);
        if (Position.Y < 0) Position = new Vector2(Position.X, Program.WORLD_HEIGHT);
        if (Position.Y > Program.WORLD_HEIGHT) Position = new Vector2(Position.X, 0);
    }
}
