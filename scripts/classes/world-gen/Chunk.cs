using System.Collections.Generic;
using Godot;

public partial class Chunk : RefCounted
{
    public Vector2I Coord;
    public TileInstance[,] Tiles;
    public List<ChunkObject> Objects;
    public List<ChunkDecor> Decors;
    public Dictionary<string, ChunkTileMeshData> DetailMeshes = new();

    public double BuildTimeMs;
    public double FinaliseTimeMs;

    public Chunk(Vector2I coord, TileInstance[,] tiles, List<ChunkObject> objects, List<ChunkDecor> decors, Dictionary<string, ChunkTileMeshData> detailMeshes)
    {
        Coord = coord;
        Tiles = tiles;
        Objects = objects;
        Decors = decors;
        DetailMeshes = detailMeshes;
    }

    public ChunkTileMeshData GetOrCreateDetailMesh(string meshId)
    {
        if (!DetailMeshes.TryGetValue(meshId, out var data))
        {
            var def = DetailMeshRegistry.Get(meshId);

            data = new ChunkTileMeshData
            {
                MeshId = meshId,
                Mesh = def.mesh,
                Material = def.material,
            };

            DetailMeshes.Add(meshId, data);
        }

        return data;
    }

    public IEnumerable<ChunkTileMeshData> GetAllDetailMeshes => DetailMeshes.Values;
}
