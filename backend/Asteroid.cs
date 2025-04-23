using System.Numerics;

public class Asteroid
{
    public int Id { get; set; }
    public Vector2 Position { get; set; }
    public Vector2 Velocity { get; set; }
    public float[] Polygon { get; set; } // 24 floats: x0, y0, x1, y1, ...
    private float _size;
    public float Size
    {
        get => _size;
        set
        {
            if (_size != value)
            {
                // Calculate size change ratio
                float change = Math.Abs(value - _size) / _size;

                if (change > 0.05f) // 5% threshold - regenerate points
                {
                    Polygon = Program.GenerateAsteroidPolygon(value);
                }
                else // Small change - just scale existing points
                {
                float scale = value / _size;
                    for (int i = 0; i < Polygon.Length; i++)
                    {
                        Polygon[i] *= scale;
                    }
                }

                _size = value;
            }
        }
    }
    public float Density { get; set; }

    // Calculated properties
    public float Mass => MathF.PI * Radius * Radius * Density; // Mass = π * r² * density
    public float KineticEnergy => 0.5f * Mass * Velocity.LengthSquared();
    public float Radius => Size / 2;

    public Asteroid(int id, Vector2 position, Vector2 velocity, float size, float[] polygon, float density = 1.0f)
    {
        Id = id;
        Position = position;
        Velocity = velocity;
        Size = size;
        Polygon = polygon;
        Density = density;
    }

    public bool IsCollidingWith(Asteroid other)
    {
        // First check if they are within collision distance
        var distance = Vector2.Distance(Position, other.Position);
        return distance < (Radius + other.Radius);
    }

    public void HandleCollision(Asteroid other)
    {
        if (!IsCollidingWith(other)) return;

        // 1. Calculate collision properties
        Vector2 normal = Vector2.Normalize(other.Position - Position);
        Vector2 relativeVelocity = other.Velocity - Velocity;
        float impactSpeed = Vector2.Dot(relativeVelocity, normal);

        // 2. Handle size reduction
        float collisionEnergy = 0.5f * (Mass * other.Mass / (Mass + other.Mass)) * impactSpeed * impactSpeed;
        
        // Each asteroid loses size based on its share of the collision energy
        float myShare = collisionEnergy * (Mass / (Mass + other.Mass));
        float otherShare = collisionEnergy * (other.Mass / (Mass + other.Mass));
        
        float myReduction = 25f * myShare / Mass;
        float otherReduction = 25f * otherShare / other.Mass;

        Console.WriteLine($"[Collision] Speed: {impactSpeed:F2}, Energy: {collisionEnergy:F1}");
        Console.WriteLine($"[Collision] Asteroid {Id}: {Size:F2} -> {Size - myReduction:F2} (-{myReduction:F2})");
        Console.WriteLine($"[Collision] Asteroid {other.Id}: {Size:F2} -> {other.Size - otherReduction:F2} (-{otherReduction:F2})");

        Size -= myReduction;
        other.Size -= otherReduction;

        // 3. Semi-elastic collision (90% energy loss)
        const float restitution = 0.5f;
        float reducedImpactSpeed = impactSpeed * restitution;

        // 4. Update velocities
        float massRatio = Mass / (Mass + other.Mass);
        Vector2 finalVelocity = Velocity + normal * (reducedImpactSpeed * (1 - massRatio));
        Vector2 otherFinalVelocity = other.Velocity - normal * (reducedImpactSpeed * massRatio);

        Velocity = finalVelocity;
        other.Velocity = otherFinalVelocity;
    }
}
