using Godot;
using System.Collections.Generic;

public class ChunkManager(World world, int chunkSize, int chunkRadius)
{
	private Dictionary<Vector2I, Chunk> ActiveChunks => world.ActiveChunks;

	private int ChunkSize { get; set; } = chunkSize;
	private int ChunkRadius { get; set; } = chunkRadius;

	public readonly HashSet<Vector2I> PendingInitialChunks = new();
	public bool InitialChunksReady;


	public void ForceInitialChunks(Vector3 playerPos)
	{
		var playerChunk = TileManager.WorldToChunk(playerPos);
		world.lastPlayerChunk = playerChunk;

		PendingInitialChunks.Clear();
		InitialChunksReady = false;

		var r = ChunkRadius;
		for (var x = -r; x <= r; x++)
		for (var y = -r; y <= r; y++)
		{
			Vector2I coord = new(playerChunk.X + x, playerChunk.Y + y);
			PendingInitialChunks.Add(coord);
		}

		RequestChunksAround(playerChunk);
	}

	public void UpdateChunks(Vector3 playerPos)
	{
		var playerChunk = TileManager.WorldToChunk(playerPos);

		if (playerChunk == world.lastPlayerChunk)
			return;

		world.lastPlayerChunk = playerChunk;

		RequestChunksAround(playerChunk);
		UnloadChunksOutside(playerChunk);
	}

	// chunk loading

	private void RequestChunksAround(Vector2I center)
	{
		var r = ChunkRadius;

		for (var x = -r; x <= r; x++)
		for (var y = -r; y <= r; y++)
		{
			Vector2I coord = new(center.X + x, center.Y + y);

			if (!world.ActiveChunks.ContainsKey(coord)) world.ChunkGenerator.RequestBuild(coord);
		}
	}

	// chunk unloading

	private void UnloadChunksOutside(Vector2I center)
	{
		var r = ChunkRadius;
		List<Vector2I> chunksToUnload = new();

		foreach (var kv in ActiveChunks)
		{
			var c = kv.Key;

			if (Mathf.Abs(c.X - center.X) > r ||
			    Mathf.Abs(c.Y - center.Y) > r)
				chunksToUnload.Add(c);
		}

		foreach (var coord in chunksToUnload)
			RemoveChunk(coord);
	}

	private void RemoveChunk(Vector2I coord)
	{
		if (!ActiveChunks.TryGetValue(coord, out var chunk))
			return;

		var c = ChunkSize;
		var baseX = coord.X * c;
		var baseY = coord.Y * c;

		var pos = new Vector3I();


		world.TryGetChunkDelta(coord, out var delta);

		for (var x = 0; x < c; x++)
		for (var y = 0; y < c; y++)
		{
			pos.X = baseX + x;
			pos.Y = 0;
			pos.Z = baseY + y;

			var tile = chunk.Tiles[x, y];

			if (tile.Definition.Name == "water")
				world.WaterMap.SetCellItem(pos, -1);
			else
				world.GroundMap.SetCellItem(pos, -1);
		}

		foreach (var obj in chunk.Objects)
		{
			if (obj.RuntimeNode is IChunkStateful<StationStateData> station)
				delta.StationStates[obj.TileCoord] = station.CaptureState();
			else if (obj.RuntimeNode is IChunkStateful<StorageStateData> storage)
				delta.StorageStates[obj.TileCoord] = storage.CaptureState();


			world.WorldObjectManager.EnqueueRemoval(obj);
		}

		// foreach (var decor in chunk.Decors)
		// {
		//     if (decor != null && GodotObject.IsInstanceValid(decor))
		//         decor.QueueFree();
		// }

		ActiveChunks.Remove(coord);
	}
}