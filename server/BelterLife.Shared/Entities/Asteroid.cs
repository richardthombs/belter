namespace BelterLife.Shared.Entities;

public class Asteroid
{
    public int Id { get; set; }
    public int SectorId { get; set; }
    public long X { get; set; }
    public long Y { get; set; }
    public float Radius { get; set; }
    public int VertexCount { get; set; }
    public float RotationOffset { get; set; }
}
