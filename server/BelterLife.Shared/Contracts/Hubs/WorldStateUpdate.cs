namespace BelterLife.Shared.Contracts.Hubs;

public record WorldStateUpdate(int SectorId, long Timestamp, List<ShipSnapshot> Ships, List<AsteroidSnapshot> Asteroids);
