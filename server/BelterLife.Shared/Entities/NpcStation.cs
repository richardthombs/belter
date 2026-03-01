namespace BelterLife.Shared.Entities;

public class NpcStation
{
    public int Id { get; set; }
    public int SectorId { get; set; }
    public long X { get; set; }
    public long Y { get; set; }
    public string Name { get; set; } = string.Empty;
}
