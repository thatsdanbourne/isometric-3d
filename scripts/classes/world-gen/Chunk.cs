using System.Collections.Generic;
using Godot;

public partial class Chunk(
	Vector2I coord,
	TileInstance[,] tiles,
	List<ChunkObject> objects,
	List<ChunkDecor> decors,
	List<ChunkMob> mobs,
	Dictionary<string, ChunkTileMeshData> detailMeshes)
{
	public Vector2I Coord = coord;
	public readonly TileInstance[,] Tiles = tiles;
	public readonly List<ChunkObject> Objects = objects;
	public List<ChunkDecor> Decors = decors;
	public List<ChunkMob> Mobs = mobs;

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