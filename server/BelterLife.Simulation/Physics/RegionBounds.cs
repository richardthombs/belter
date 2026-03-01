namespace BelterLife.Simulation.Physics;

/// <summary>
/// Canonical sector geometry constants. 1 unit = 1 mm. Sector = 50km × 50km square.
/// Every shard uses these constants — sector size is fixed and never changes.
/// </summary>
public static class RegionBounds
{
	/// <summary>Side length of one sector in mm (50 km).</summary>
	public const long SectorSize = 50_000_000L;

	/// <summary>Half the sector side — the local coordinate range is [-HalfSector, +HalfSector).</summary>
	public const long HalfSector = SectorSize / 2L;
}
