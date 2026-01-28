using Godot;
using System.Collections.Generic;

public class ChunkDeltaData
{
    public HashSet<Vector2I> RemovedProceduralObjects = new();
    public List<ChunkObject> PlacedObjects = new();
}
