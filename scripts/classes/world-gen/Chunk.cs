using System.Collections.Generic;
using Godot;

public partial class Chunk : RefCounted
{
    public Vector2I Coord;
    public TileInstance[,] Tiles;
    public List<ChunkObject> Objects;
    public List<ChunkDecor> Decors;
    public List<ChunkTileMeshData> TileMeshData = new();

    public double BuildTimeMs;
    public double FinaliseTimeMs;

    public Chunk(Vector2I coord, TileInstance[,] tiles, List<ChunkObject> objects, List<ChunkDecor> decors, List<ChunkTileMeshData> tileMeshData = null)
    {
        Coord = coord;
        Tiles = tiles;
        Objects = objects;
        Decors = decors;
        TileMeshData = tileMeshData;
    }
}
