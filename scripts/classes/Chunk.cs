using Godot;

public partial class Chunk : RefCounted
{
    public Vector2I Coord;
    public Tile[,] Tiles;
    public Godot.Collections.Array<Node3D> Objects = new();

    public Chunk(int size)
    {
        Tiles = new Tile[size, size];
    }
}
