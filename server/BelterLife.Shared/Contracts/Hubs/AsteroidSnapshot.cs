namespace BelterLife.Shared.Contracts.Hubs;

public record AsteroidSnapshot(int AsteroidId, long X, long Y, float Radius, int VertexCount, float RotationOffset);
