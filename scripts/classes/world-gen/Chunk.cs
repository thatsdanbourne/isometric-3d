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
}

public class ChunkSpawnContext
{
	public BiomeDefinition[,] BaseBiomes { get; init; }
	public BiomeDefinition[,] FinalBiomes { get; init; }
	public WaterFeatureType[,] WaterFeatures { get; init; }
	public int[,] Objects { get; init; }
}