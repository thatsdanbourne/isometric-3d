using System.Collections.Generic;
using Godot;

public partial class Chunk : RefCounted
{
    public Vector2I Coord;
    public Tile[,] Tiles;
    public List<ChunkObject> Objects;
    public List<ChunkDecor> Decors;

    public double BuildTimeMs;
    public double FinaliseTimeMs;

    public Chunk(Vector2I coord, Tile[,] tiles, List<ChunkObject> objects, List<ChunkDecor> decors)
    {
        Coord = coord;
        Tiles = tiles;
        Objects = objects;
        Decors = decors;
    }
}
