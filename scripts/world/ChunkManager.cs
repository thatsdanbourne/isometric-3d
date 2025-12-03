using Godot;
using System.Collections.Generic;

public class ChunkManager
{
    private readonly World _world;

    public Dictionary<Vector2I, Chunk> ActiveChunks => _world.ActiveChunks;

    public int ChunkSize { get; set; }
    public int ChunkRadius { get; set; }

    public ChunkManager(World world, int chunkSize, int chunkRadius)
    {
        _world = world;
        ChunkSize = chunkSize;
        ChunkRadius = chunkRadius;
    }



    public void UpdateChunks(Vector3 playerPos)
    {
        Vector2I playerChunk = TileManager.WorldToChunk(playerPos);

        if (playerChunk == _world.lastPlayerChunk)
            return;

        _world.lastPlayerChunk = playerChunk;

        RequestChunksAround(playerChunk);
        UnloadChunksOutside(playerChunk);
    }

    // chunk loading

    public void RequestChunksAround(Vector2I center)
    {
        int r = ChunkRadius;

        for (int x = -r; x <= r; x++)
        {
            for (int y = -r; y <= r; y++)
            {
                Vector2I coord = new(center.X + x, center.Y + y);

                if (!_world.ActiveChunks.ContainsKey(coord))
                {
                    _world.buildQueue.Enqueue(coord);
                }
            }
        }
    }

    // chunk unloading

    public void UnloadChunksOutside(Vector2I center)
    {
        int r = ChunkRadius;
        List<Vector2I> chunksToUnload = new();

        foreach (var kv in ActiveChunks)
        {
            Vector2I c = kv.Key;

            if (Mathf.Abs(c.X - center.X) > r ||
                Mathf.Abs(c.Y - center.Y) > r)
            {
                chunksToUnload.Add(c);
            }
        }

        foreach (var coord in chunksToUnload)
            RemoveChunk(coord);
    }

    public void RemoveChunk(Vector2I coord)
    {
        if (!ActiveChunks.TryGetValue(coord, out Chunk chunk))
            return;
        
        int C = ChunkSize;
        int baseX = coord.X * C;
        int baseY = coord.Y * C;

        Vector3I pos = new Vector3I();

        for (int x = 0; x < C; x++)
        {
            for (int y = 0; y < C; y++)
            {
                pos.X = baseX + x;
                pos.Y = 0;
                pos.Z = baseY + y;

                var tile = chunk.Tiles[x, y];

                if (tile.Type == "water")
                    _world.WaterMap.SetCellItem(pos, -1);
                else
                    _world.GroundMap.SetCellItem(pos, -1);
            }
        }

        foreach (var obj in chunk.Objects)
        {
            if (obj != null && GodotObject.IsInstanceValid(obj))
                obj.QueueFree();
        }

        foreach (var decor in chunk.Decors)
        {
            if (decor != null && GodotObject.IsInstanceValid(decor))
                decor.QueueFree();
        }

        ActiveChunks.Remove(coord);
    }

    // adding chunk objects

    public void AddChunkObject(WorldObject obj)
    {
        Vector2I coords = TileManager.WorldToChunk(obj.Position);

        if (ActiveChunks.TryGetValue(coords, out Chunk chunk))
        {
            chunk.Objects.Add(obj);
            obj.Chunk = chunk;
        }
        else
        {
            GD.PrintErr("Tried to add object to non-existent chunk at " + coords);
        }
    }

    // removing chunk objects

    public void RemoveChunkObject(Node3D obj)
    {
        if (obj is WorldObject wo && wo.Chunk != null)
        {
            wo.Chunk.Objects.Remove(wo);
            return;
        }

        foreach (var chunk in ActiveChunks.Values)
        {
            if (chunk.Objects.Contains(obj))
            {
                chunk.Objects.Remove(obj);
                return;
            }
        }
    }
}
