using Godot;
using System.Collections.Generic;
using System.Data.Common;

public partial class WorldObjectManager : Node
{
    public int MaxSpawnsPerFrame = 6;
    public int MaxRemovesPerFrame = 10;

    private readonly Queue<Chunk> spawnChunkQueue = new();
    private readonly Queue<ChunkObject> activeSpawnQueue = new();
    private readonly Queue<ChunkObject> removeQueue = new();

    private Dictionary<string, Stack<WorldObject>> pools = new();

    private World _world;
    private RandomNumberGenerator rng;

    private int maxPoolSizePerType = 64;


    public override void _Ready()
    {
        _world = GetParent<World>();
        rng = new RandomNumberGenerator();
        rng.Randomize();
    }

    public override void _Process(double delta)
    {
        ProcessSpawns();
        ProcessRemovals();
    }

    public void EnqueueChunk(Chunk chunk)
    {
        spawnChunkQueue.Enqueue(chunk);
    }

    public void EnqueueSpawn(ChunkObject data)
    {
        if (data.RuntimeNode != null || data.MarkedForRemoval)
            return;

        activeSpawnQueue.Enqueue(data);
    }

    public void EnqueueRemoval(ChunkObject data)
    {
        if (data.MarkedForRemoval)
            return;

        data.MarkedForRemoval = true;
        removeQueue.Enqueue(data);
    }

    private void ProcessSpawns()
    {
        int count = 0;

        if (activeSpawnQueue.Count == 0 && spawnChunkQueue.Count > 0)
        {
            var chunk = spawnChunkQueue.Dequeue();

            foreach (var obj in chunk.Objects)
                activeSpawnQueue.Enqueue(obj);
        }

        while (activeSpawnQueue.Count > 0 && count < MaxSpawnsPerFrame)
        {
            var data = activeSpawnQueue.Dequeue();

            if (data.MarkedForRemoval || data.RuntimeNode != null)
                continue;

            var node = GetPooled(data.Definition.Id);

            node.Reset();
            node.Data = data;
            node.World = _world;
            node.RequiredTier = data.Definition.ToolTier;
            data.RuntimeNode = node;

            if (node.GetParent() != null)
                node.Reparent(_world.WorldObjects);
            else
                _world.WorldObjects.AddChild(node);

            node.Visible = true;
            node.SetPhysicsProcess(true);

            node.GlobalPosition = data.Position;
            node.Translate(new Vector3(0f, 0f, rng.RandfRange(-0.01f, 0.01f)));

            if (data.Definition.BlocksTile)
            {
                _world.BlockTile(data.TileCoord);
                node.EnableCollision();
            }
            count++;
        }
    }

    private void ProcessRemovals()
    {
        int count = 0;

        while (removeQueue.Count > 0 && count < MaxRemovesPerFrame)
        {
            var data = removeQueue.Dequeue();

            if (data.RuntimeNode != null)
            {
                if (data.Definition.BlocksTile)
                    _world.UnblockTile(data.TileCoord);

                Recycle(data.RuntimeNode);
                data.RuntimeNode = null;
            }

            count++;
        }
    }

    private WorldObject GetPooled(string sceneId)
    {
        if (pools.TryGetValue(sceneId, out var stack) && stack.Count > 0)
            return stack.Pop();

        return WorldObjectRegistry.GetScene(sceneId).Instantiate<WorldObject>();
    }

    private void Recycle(WorldObject node)
    {
        node.DisableCollision();
        node.Visible = false;
        node.SetPhysicsProcess(false);
        node.Reparent(_world.WorldObjectPool);

        var id = node.Data.Definition.Id;

        if (!pools.TryGetValue(id, out var stack))
            pools[id] = stack = new Stack<WorldObject>();

        if (stack.Count < maxPoolSizePerType)
            stack.Push(node);
        else
            node.QueueFree();
    }
}
