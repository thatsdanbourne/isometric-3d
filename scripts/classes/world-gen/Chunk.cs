using System.Collections.Generic;
using Godot;

public class Chunk(
	Vector2I coord,
	TileInstance[,] tiles,
	List<ChunkObject> objects
)
{
	public Vector2I Coord = coord;
	public readonly TileInstance[,] Tiles = tiles;
	public readonly List<ChunkObject> Objects = objects;
	public Dictionary<Vector2I, StorageStateData> StorageStates = new();
	public Dictionary<Vector2I, StationStateData> StationStates = new();

	public ChunkSpawnContext SpawnContext;

	public double BuildTimeMs;
	public double FinaliseTimeMs;
}

public class ChunkSpawnContext
{
	public required BiomeDefinition[,] BaseBiomes { get; init; }
	public required BiomeDefinition[,] FinalBiomes { get; init; }
	public required WaterFeatureType[,] WaterFeatures { get; init; }
}