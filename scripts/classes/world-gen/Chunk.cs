using System.Collections.Generic;
using Godot;

public partial class Chunk(
	Vector2I coord,
	TileInstance[,] tiles,
	List<ChunkObject> objects,
	List<ChunkDecor> decors,
	Dictionary<string, ChunkTileMeshData> detailMeshes)
{
	public Vector2I Coord = coord;
	public readonly TileInstance[,] Tiles = tiles;
	public readonly List<ChunkObject> Objects = objects;
	public List<ChunkDecor> Decors = decors;

	public ChunkSpawnContext SpawnContext;

	public double BuildTimeMs;
	public double FinaliseTimeMs;

	private ChunkTileMeshData GetOrCreateDetailMesh(string meshId)
	{
		if (detailMeshes.TryGetValue(meshId, out var data)) return data;
		var def = DetailMeshRegistry.Get(meshId);

		data = new ChunkTileMeshData
		{
			MeshId = meshId,
			Mesh = def.mesh,
			Material = def.material
		};

		detailMeshes.Add(meshId, data);

		return data;
	}

	public IEnumerable<ChunkTileMeshData> GetAllDetailMeshes => detailMeshes.Values;
}

public class ChunkSpawnContext
{
	public required BiomeDefinition[,] BaseBiomes { get; init; }
	public required BiomeDefinition[,] FinalBiomes { get; init; }
	public required WaterFeatureType[,] WaterFeatures { get; init; }
}