using Godot;
using System.Collections.Generic;

public class ChunkManager(World world, int chunkSize, int chunkRadius)
{
	private Dictionary<Vector2I, Chunk> ServerChunks => world.ServerChunks;
	private Dictionary<Vector2I, Chunk> ActiveChunks => world.ActiveChunks;
	private readonly Dictionary<int, HashSet<Vector2I>> _desiredChunksByPeer = new();
	private readonly Dictionary<int, HashSet<Vector2I>> _sentChunksByPeer = new();

	private int ChunkSize { get; } = chunkSize;
	private int ChunkRadius { get; } = chunkRadius;

	private Vector2I? _lastLocalPlayerChunk;
	private readonly HashSet<Vector2I> _pendingBuilds = [];


	public void UpdateAuthorityChunks(IReadOnlyList<Vector3> playerPositions)
	{
		foreach (var player in playerPositions)
			UpdateLocalChunks(player);
	}

	public void UpdateServerChunkCache(IReadOnlyList<Vector3> playerPositions)
	{
		if (playerPositions == null || playerPositions.Count == 0)
			return;

		var playerChunks = GetUniquePlayerChunks(playerPositions);
		var desiredChunks = BuildDesiredChunkSet(playerChunks);

		EnsureServerChunksExist(desiredChunks);
		EvictUnusedServerChunks(desiredChunks);
	}

	public void InvalidateServerChunk(Vector2I coord)
	{
		ServerChunks.Remove(coord);
	}

	public void EvictUnusedServerChunks(HashSet<Vector2I> neededCoords)
	{
		var toRemove = new List<Vector2I>();

		foreach (var coord in ServerChunks.Keys)
			if (!neededCoords.Contains(coord))
				toRemove.Add(coord);

		foreach (var coord in toRemove)
			ServerChunks.Remove(coord);
	}

	public void UpdateLocalChunks(Vector3 localPlayerPosition)
	{
		var playerChunk = TileUtils.WorldToChunk(localPlayerPosition);

		var currentPlayerChunks = new HashSet<Vector2I> { playerChunk };
		var desiredChunks = BuildDesiredChunkSet(currentPlayerChunks);

		EnsureLocalChunksActive(desiredChunks);

		if (_lastLocalPlayerChunk.HasValue && _lastLocalPlayerChunk.Value == playerChunk)
			return;

		_lastLocalPlayerChunk = playerChunk;
		UnloadChunksOutsideDesiredSet(desiredChunks);
	}

	private void EnsureServerChunksExist(HashSet<Vector2I> desiredChunks)
	{
		foreach (var coord in desiredChunks)
		{
			if (ServerChunks.ContainsKey(coord))
				continue;

			if (!_pendingBuilds.Add(coord))
				continue;

			world.ChunkGenerator.RequestBuild(coord);
		}
	}

	private void EnsureLocalChunksActive(HashSet<Vector2I> desiredChunks)
	{
		foreach (var coord in desiredChunks)
		{
			if (ActiveChunks.ContainsKey(coord))
				continue;

			if (ServerChunks.TryGetValue(coord, out var chunk))
				world.ChunkGenerator.FinaliseChunk(chunk);
		}
	}

	public void OnServerChunkBuilt(Vector2I coord)
	{
		_pendingBuilds.Remove(coord);

		if (!ServerChunks.TryGetValue(coord, out var chunk))
			return;

		foreach (var kv in _desiredChunksByPeer)
		{
			var peerId = kv.Key;
			var desiredChunks = kv.Value;

			if (!desiredChunks.Contains(coord))
				continue;

			if (!_sentChunksByPeer.TryGetValue(peerId, out var sentChunks))
			{
				sentChunks = new HashSet<Vector2I>();
				_sentChunksByPeer[peerId] = sentChunks;
			}

			if (sentChunks.Contains(coord))
				continue;

			world.Sync.SendChunkToPeer(peerId, ToDto(chunk));
			sentChunks.Add(coord);
		}
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

	public IEnumerable<int> GetPeersInterestedInChunk(Vector2I coord)
	{
		foreach (var kv in _desiredChunksByPeer)
			if (kv.Value.Contains(coord))
				yield return kv.Key;
	}

	public void UpdatePeerInterest(int peerId, Godot.Collections.Array<Vector2I> coords)
	{
		if (!_desiredChunksByPeer.TryGetValue(peerId, out var desiredChunks))
		{
			desiredChunks = new HashSet<Vector2I>();
			_desiredChunksByPeer[peerId] = desiredChunks;
		}

		desiredChunks.Clear();
		foreach (var coord in coords)
			desiredChunks.Add(coord);

		if (!_sentChunksByPeer.TryGetValue(peerId, out var sentChunks))
		{
			sentChunks = new HashSet<Vector2I>();
			_sentChunksByPeer[peerId] = sentChunks;
		}

		var toUnload = new List<Vector2I>();
		foreach (var coord in sentChunks)
			if (!desiredChunks.Contains(coord))
				toUnload.Add(coord);

		foreach (var coord in toUnload)
		{
			world.Sync.SendChunkUnloadToPeer(peerId, coord);
			sentChunks.Remove(coord);
		}

		EnsureServerChunksExist(desiredChunks);

		var newlyAddedChunks = new HashSet<Vector2I>();
		foreach (var coord in desiredChunks)
		{
			if (sentChunks.Contains(coord))
				continue;

			if (!ServerChunks.TryGetValue(coord, out var chunk))
				continue;

			world.Sync.SendChunkToPeer(peerId, ToDto(chunk));
			newlyAddedChunks.Add(coord);
			sentChunks.Add(coord);
		}

		// if (newlyAddedChunks.Count > 0)
		// 	world.MobStreamer.SyncActiveMobsInChunksToPeer(peerId, newlyAddedChunks);
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

	public void RemoveChunk(Vector2I coord)
	{
		if (!ActiveChunks.TryGetValue(coord, out var chunk))
			return;

		var c = ChunkSize;
		var baseX = coord.X * c;
		var baseY = coord.Y * c;

		var pos = new Vector3I();

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
			world.WorldObjectManager.EnqueueRemoval(obj);

		ActiveChunks.Remove(coord);
	}

	public void ForgetPeerChunks(int peerId)
	{
		_desiredChunksByPeer.Remove(peerId);
		_sentChunksByPeer.Remove(peerId);
	}

	// utils

	private ChunkDto ToDto(Chunk chunk)
	{
		var width = chunk.Tiles.GetLength(0);
		var height = chunk.Tiles.GetLength(1);

		var tiles = new List<TileInstanceDto>(width * height);
		for (var x = 0; x < width; x++)
		for (var y = 0; y < height; y++)
		{
			var tile = chunk.Tiles[x, y];

			tiles.Add(new TileInstanceDto
			{
				X = x,
				Y = y,
				DefinitionId = (int)tile.Definition.Id,
				BiomeId = (int)tile.Biome,
				Temperature = tile.Temp,
				Humidity = tile.Humidity
			});
		}

		var objects = new List<ChunkObjectDto>(chunk.Objects.Count);
		foreach (var obj in chunk.Objects)
			objects.Add(new ChunkObjectDto
			{
				DefinitionId = obj.Definition.StableId,
				ChunkCoord = obj.ChunkCoord,
				TileCoord = obj.TileCoord,
				Position = obj.Position,
				Source = obj.Source
			});

		var dto = new ChunkDto(
			chunk.Coord,
			tiles,
			objects
		);

		if (world.TryGetChunkDelta(chunk.Coord, out var delta))
		{
			foreach (var kv in delta.StorageStates)
				dto.StorageStates[kv.Key] = kv.Value;

			foreach (var kv in delta.StationStates)
				dto.StationStates[kv.Key] = kv.Value;
		}

		return dto;
	}
}