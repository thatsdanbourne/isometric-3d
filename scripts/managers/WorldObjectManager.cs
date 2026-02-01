using Godot;
using System.Collections.Generic;

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

    private PackedScene pickupScene;


    public override void _Ready()
    {
        _world = GetParent<World>();
        rng = new RandomNumberGenerator();
        rng.Randomize();
        pickupScene = ResourceLoader.Load<PackedScene>("res://scenes/ItemPickup.tscn");
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

    public void RequestBreak(ChunkObject data)
    {
        if (data.RuntimeNode is IItemContainer storage)
        {
            foreach (var stack in storage.GetSlots())
            {
                if (stack == null || stack.Count <= 0) continue;

                ItemPickup pickup = pickupScene.Instantiate<ItemPickup>();
                pickup.Item = stack.Item;
                pickup.Count = stack.Count;

                _world.ItemPickupContainer.AddChild(pickup);
                pickup.GlobalPosition = data.Position;
            }
        }

        SpawnDrops(data);

        // clear chunk delta states
        var delta = _world.GetOrCreateChunkDelta(data.ChunkCoord);

        if (data.Source == ChunkObjectSource.Procedural)
            delta.RemovedProceduralObjects.Add(data.TileCoord);
        else if (data.Source == ChunkObjectSource.Placed)
            delta.PlacedObjects.Remove(data);

        if (delta.StorageStates.ContainsKey(data.TileCoord))
            delta.StorageStates.Remove(data.TileCoord);

        EnqueueRemoval(data);
    }

    private void SpawnDrops(ChunkObject data)
    {
        if (data.RuntimeNode is not WorldObject wo)
            return;

        var drops = wo.DropItems;
        if (drops == null || drops.Count == 0) return;

        foreach (var entry in drops)
        {
            if (GD.Randf() > entry.Chance)
                continue;

            var item = ItemRegistry.GetItem(entry.ItemId);
            if (item == null) continue;

            int quantity = rng.RandiRange(entry.MinQuantity, entry.MaxQuantity);

            for (int n = 0; n < quantity; n++)
            {
                ItemPickup pickup = pickupScene.Instantiate<ItemPickup>();
                pickup.Item = item;

                _world.ItemPickupContainer.AddChild(pickup);
                pickup.GlobalPosition = data.Position;
            }
        }
    }

    public bool RequestPlace(ChunkObject data)
    {
        var chunk = _world.ActiveChunks[data.ChunkCoord];

        chunk.Objects.Add(data);
        EnqueueSpawn(data);

        var chunkDelta = _world.GetOrCreateChunkDelta(data.ChunkCoord);
        chunkDelta.PlacedObjects.Add(data);

        return true;
    }

    public void EnqueueRemoval(ChunkObject data)
    {
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

            WorldObject node = WorldObjectRegistry.GetScene(data.Definition.Id).Instantiate<WorldObject>();

            node.Reset();
            node.Data = data;
            node.World = _world;
            data.RuntimeNode = node;

            node.Initialise(data.Definition);

            if (node.GetParent() != null)
                node.Reparent(_world.WorldObjects);
            else
                _world.WorldObjects.AddChild(node);

            node.Visible = true;
            node.SetPhysicsProcess(true);

            node.GlobalPosition = data.Position;
            node.Translate(new Vector3(0f, 0f, rng.RandfRange(-0.01f, 0.01f)));

            if (data.Definition.BlocksTile)
                _world.BlockTile(data.TileCoord);

            // Restore state if applicable
            ChunkDeltaData delta;
            _world.TryGetChunkDelta(data.ChunkCoord, out delta);

            if (node is IChunkStateful<StationStateData> station)
            {
                if (delta.StationStates.TryGetValue(data.TileCoord, out var stationState))
                    station.RestoreState(stationState);
            }
            else if (node is IChunkStateful<StorageStateData> storage)
            {
                if (delta.StorageStates.TryGetValue(data.TileCoord, out var storageState))
                    storage.RestoreState(storageState);
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
        // node.Visible = false;
        // node.SetPhysicsProcess(false);
        // node.Reparent(_world.WorldObjectPool);

        // var id = node.Data.Definition.Id;

        // if (!pools.TryGetValue(id, out var stack))
        //     pools[id] = stack = new Stack<WorldObjectBase>();

        // if (stack.Count < maxPoolSizePerType)
        //     stack.Push(node);
        // else
        //     node.QueueFree();

        node.QueueFree();
    }
}
