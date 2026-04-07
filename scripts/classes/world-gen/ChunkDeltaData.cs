using Godot;
using System.Collections.Generic;

public class ChunkDeltaData
{
	public HashSet<Vector2I> RemovedProceduralObjects = new();
	public Dictionary<Vector2I, PlacedObjectRecord> PlacedObjectsByTile = new();
	public Dictionary<Vector2I, StationStateData> StationStates = new();
	public Dictionary<Vector2I, StorageStateData> StorageStates = new();
	public Dictionary<ulong, MobRecord> Mobs = new();
}

public struct PlacedObjectRecord
{
	public int DefinitionTypeId;
	public Vector2I TileCoord;
	public Vector3 Position;
}

public class StationStateData
{
	public int ObjectId;
	public Vector2I TileCoord;
	public string ActiveRecipeId;
	public float TimeRemaining;
	public int CompletedCount;
	public int TotalCount;
	public bool IsCrafting;

	public double LastUpdateTime;
}

public class StorageStateData
{
	public int ObjectId;
	public Vector2I TileCoord;
	public ItemStack[] Slots;
}

public class MobRecord
{
	public ulong Uid;
	public string MobId;
	public Vector3 Position;
}