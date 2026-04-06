using Godot;

public partial class ChunkObject : RefCounted
{
	public WorldObjectDefinition Definition;
	public Vector2I ChunkCoord;
	public Vector2I TileCoord;
	public Vector3 Position;

	public ChunkObjectSource Source;

	// runtime
	public WorldObject RuntimeNode;
	public bool MarkedForRemoval = false;
}

public enum ChunkObjectSource
{
	Procedural,
	Placed
}