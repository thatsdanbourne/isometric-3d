using Godot;
using System.Collections.Generic;
using System.Data.Common;

public partial class WorldObjectManager : Node
{
    public int MaxSpawnsPerFrame = 6;
    public int MaxRemovesPerFrame = 10;

    private readonly Queue<ChunkObject> spawnQueue = new();
    private readonly Queue<ChunkObject> removeQueue = new();

    private World _world;
    private RandomNumberGenerator rng;


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

    public void EnqueueSpawn(ChunkObject data)
    {
        if (data.RuntimeNode != null || data.MarkedForRemoval)
            return;

        spawnQueue.Enqueue(data);
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

        while (spawnQueue.Count > 0 && count < MaxSpawnsPerFrame)
        {
            var data = spawnQueue.Dequeue();

            if (data.MarkedForRemoval)
                continue;

            var node = data.Definition.Scene.Instantiate<WorldObject>();
            node.Data = data;

            _world.WorldObjects.AddChild(node);
            data.RuntimeNode = node;
            node.GlobalPosition = data.Position;
            node.World = _world;

            if (data.Definition.BlocksTile)
                _world.BlockTile(data.TileCoord);

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
                _world.UnblockTile(data.TileCoord);
                data.RuntimeNode.QueueFree();
                data.RuntimeNode = null;
            }

            count++;
        }
    }

    // public SpawnVariant PickObjectVariant(ObjectSpawnRule rule, List<SpawnVariant> allowedVariants, int x, int z)
    // {
    //     var valid = allowedVariants.Count > 0 ? allowedVariants : rule.Variants;
    //     if (valid.Count == 0) return null;

    //     int hash = (x * 73856093) ^ (z * 19349663) ^ rule.GetHashCode();
    //     rng.Seed = (ulong)hash;

    //     float total = 0f;
    //     foreach (var v in valid)
    //         total += v.Weight;

    //     float r = rng.Randf() * total;

    //     foreach (var v in valid)
    //     {
    //         if (r <= v.Weight)
    //             return v;

    //         r -= v.Weight;
    //     }

    //     return rule.Variants[0];
    // }
}
