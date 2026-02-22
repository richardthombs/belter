namespace BelterLife.Shared.Contracts.Hubs;

public record ShipSnapshot(int ShipId, string PlayerId, float X, float Y, float VelocityX, float VelocityY, float Heading);
