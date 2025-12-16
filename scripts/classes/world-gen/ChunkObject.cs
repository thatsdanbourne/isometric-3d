using Godot;

public partial class ChunkObject : RefCounted
{
    public WorldObjectDefinition Definition;
    public Vector2I ChunkCoord;
    public Vector2I TileCoord;
    public Vector3 Position;

    // runtime
    public WorldObject RuntimeNode;
    public bool MarkedForRemoval;
}

public partial class ChunkDecor : RefCounted
{
    public Vector3 Position;
    public DecorSpawnRule DecorRule;
}
