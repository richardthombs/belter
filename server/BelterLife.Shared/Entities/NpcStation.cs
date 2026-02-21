namespace BelterLife.Shared.Entities;

public class NpcStation
{
    public int Id { get; set; }
    public int SectorId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public string Name { get; set; } = string.Empty;
}
