namespace BelterLife.Shared.Entities;

public class Sector
{
    public int Id { get; set; }
    public long GridX { get; set; }
    public long GridY { get; set; }
    public bool IsGenerated { get; set; }
    public long Seed { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
