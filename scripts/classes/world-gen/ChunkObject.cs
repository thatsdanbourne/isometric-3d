using Godot;

public partial class ChunkObject : RefCounted
{
    public Vector2I TileCoord;
    public Vector3 Position;
    public ObjectSpawnRule ObjectRule;
}

public partial class ChunkDecor : RefCounted
{
    public Vector3 Position;
    public DecorSpawnRule DecorRule;
}
