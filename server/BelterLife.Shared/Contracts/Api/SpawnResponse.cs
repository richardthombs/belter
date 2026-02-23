namespace BelterLife.Shared.Contracts.Api;

public record SpawnResponse(int SectorId, int ShipId, float SpawnX, float SpawnY, bool Repositioned = false);
