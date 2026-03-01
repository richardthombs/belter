namespace BelterLife.Shared.Contracts.Api;

public record SpawnResponse(int SectorId, int ShipId, long SpawnX, long SpawnY, bool Repositioned = false);
