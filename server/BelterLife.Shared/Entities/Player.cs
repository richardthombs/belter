namespace BelterLife.Shared.Entities;

public class Player
{
	public string Id { get; set; } = string.Empty; // matches IdentityUser.Id
	public int SectorId { get; set; }
	public int ShipId { get; set; }
	public DateTimeOffset LastSeenAt { get; set; }
	public int Credits { get; set; }
}
