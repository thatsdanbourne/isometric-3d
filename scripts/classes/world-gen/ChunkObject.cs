using Godot;

public partial class ChunkObject : RefCounted
{
    public Vector3 Position;
    public BiomeObjectSpawnRule BiomeRule;
}

public partial class ChunkDecor : RefCounted
{
    public Vector3 Position;
    public DecorPlacementRule Rule;
}
