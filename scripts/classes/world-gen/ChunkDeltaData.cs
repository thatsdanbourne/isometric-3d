using Godot;
using System.Collections.Generic;

public class ChunkDeltaData
{
    public HashSet<Vector2I> RemovedProceduralObjects = new();
    public List<ChunkObject> PlacedObjects = new();
    public Dictionary<Vector2I, StationStateData> StationStates = new();
}

public class StationStateData
{
    public string ObjectId;
    public Vector2I TileCoord;

    public string ActiveRecipeId;
    public float TimeRemaining;
    public int CompletedCount;
    public int TotalCount;
    public bool IsCrafting;

    public double LastUpdateTime;
}
