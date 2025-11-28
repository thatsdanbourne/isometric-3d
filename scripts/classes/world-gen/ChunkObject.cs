using Godot;

public partial class ChunkObject : RefCounted
{
    public Vector3 Position;
    public ObjectPlacementRule Rule;
}

public partial class ChunkDecor : RefCounted
{
    public Vector3 Position;
    public DecorPlacementRule Rule;
}
