using Godot;
using System.Collections.Generic;

public class ChunkManager(World world, int chunkSize, int chunkRadius)
{
	private Dictionary<Vector2I, Chunk> ActiveChunks => world.ActiveChunks;

	private int ChunkSize { get; } = chunkSize;
	private int ChunkRadius { get; } = chunkRadius;

	public readonly HashSet<Vector2I> PendingInitialChunks = [];
	public bool InitialChunksReady;

	private readonly HashSet<Vector2I> _lastPlayerChunks = [];


	public void PreloadChunks(IReadOnlyList<Vector3> positions)
	{
		if (positions == null || positions.Count == 0)
		{
			PendingInitialChunks.Clear();
			InitialChunksReady = true;
			return;
		}

		var centers = GetUniquePlayerChunks(positions);
		var desiredChunks = BuildDesiredChunkSet(centers);

		PendingInitialChunks.Clear();
		foreach (var coord in desiredChunks)
			if (!world.ActiveChunks.ContainsKey(coord))
				PendingInitialChunks.Add(coord);

		InitialChunksReady = PendingInitialChunks.Count == 0;

		RequestMissingChunks(desiredChunks);
	}

	public void PreloadChunks(Vector3 position)
	{
		PreloadChunks([position]);
	}

	public void UpdateChunks(IReadOnlyList<Vector3> playerPositions)
	{
		if (playerPositions == null || playerPositions.Count == 0) return;

		var playerChunks = GetUniquePlayerChunks(playerPositions);

		if (_lastPlayerChunks.SetEquals(playerChunks)) return;

		_lastPlayerChunks.Clear();
		_lastPlayerChunks.UnionWith(playerChunks);

		var desiredChunks = BuildDesiredChunkSet(playerChunks);

		RequestMissingChunks(desiredChunks);
		UnloadChunksOutsideDesiredSet(desiredChunks);
	}

	private HashSet<Vector2I> GetUniquePlayerChunks(IReadOnlyList<Vector3> playerPositions)
	{
		var result = new HashSet<Vector2I>();

		foreach (var pos in playerPositions)
			result.Add(TileUtils.WorldToChunk(pos));

		return result;
	}

	private HashSet<Vector2I> BuildDesiredChunkSet(HashSet<Vector2I> playerChunks)
	{
		var desired = new HashSet<Vector2I>();

		foreach (var chunk in playerChunks)
			for (var x = -ChunkRadius; x <= ChunkRadius; x++)
			for (var y = -ChunkRadius; y <= ChunkRadius; y++)
				desired.Add(new Vector2I(chunk.X + x, chunk.Y + y));

		return desired;
	}

	private void RequestMissingChunks(HashSet<Vector2I> desiredChunks)
	{
		foreach (var coord in desiredChunks)
			if (!ActiveChunks.ContainsKey(coord))
				world.ChunkGenerator.RequestBuild(coord);
	}

	private void UnloadChunksOutsideDesiredSet(HashSet<Vector2I> desiredChunks)
	{
		List<Vector2I> chunksToUnload = [];

		foreach (var kv in ActiveChunks)
		{
			var coord = kv.Key;

			if (!desiredChunks.Contains(coord))
				chunksToUnload.Add(coord);
		}

		foreach (var coord in chunksToUnload)
			RemoveChunk(coord);
	}


	// chunk unloading

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
			switch (obj.RuntimeNode)
			{
				case IChunkStateful<StationStateData> station:
					delta.StationStates[obj.TileCoord] = station.CaptureState();
					break;
				case IChunkStateful<StorageStateData> storage:
					delta.StorageStates[obj.TileCoord] = storage.CaptureState();
					break;
			}


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