namespace BelterLife.Shared.Entities;

public class Ship
{
    public int Id { get; set; }
    public string PlayerId { get; set; } = string.Empty;
    public int SectorId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float VelocityX { get; set; }
    public float VelocityY { get; set; }
    public float Heading { get; set; }
}
