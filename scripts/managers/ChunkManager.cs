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

	private readonly HashSet<Vector2I> _lastLocalPlayerChunks = [];
	private readonly HashSet<Vector2I> _pendingBuilds = [];


	public void UpdateServerChunkCache(IReadOnlyList<Vector3> playerPositions)
	{
		if (playerPositions == null || playerPositions.Count == 0)
			return;

		var playerChunks = GetUniquePlayerChunks(playerPositions);
		var desiredChunks = BuildDesiredChunkSet(playerChunks);

		EnsureServerChunksExist(desiredChunks);
	}

	public void UpdateLocalChunks(IReadOnlyList<Vector3> localPlayerPositions)
	{
		if (localPlayerPositions == null || localPlayerPositions.Count == 0) return;

		var playerChunks = GetUniquePlayerChunks(localPlayerPositions);
		var desiredChunks = BuildDesiredChunkSet(playerChunks);

		EnsureLocalChunksActive(desiredChunks);

		if (!_lastLocalPlayerChunks.SetEquals(playerChunks))
		{
			_lastLocalPlayerChunks.Clear();
			_lastLocalPlayerChunks.UnionWith(playerChunks);
			UnloadChunksOutsideDesiredSet(desiredChunks);
		}
	}

	private void EnsureServerChunksExist(HashSet<Vector2I> desiredChunks)
	{
		foreach (var coord in desiredChunks)
		{
			if (ServerChunks.ContainsKey(coord))
				continue;

			if (_pendingBuilds.Contains(coord))
				continue;

			_pendingBuilds.Add(coord);
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

			world.SendChunkToPeer(peerId, ToDto(chunk));
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

	public void UpdatePeerInterest(int peerId, Godot.Collections.Array<Vector2I> coords)
	{
		if (!world.Multiplayer.IsServer())
			return;

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
			world.SendChunkUnloadToPeer(peerId, coord);
			sentChunks.Remove(coord);
		}

		EnsureServerChunksExist(desiredChunks);

		foreach (var coord in desiredChunks)
		{
			if (sentChunks.Contains(coord))
				continue;

			if (!ServerChunks.TryGetValue(coord, out var chunk))
				continue;

			world.SendChunkToPeer(peerId, ToDto(chunk));
			sentChunks.Add(coord);
		}
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

		return new ChunkDto(
			chunk.Coord,
			tiles,
			objects
		);
	}

	public Godot.Collections.Dictionary SerializeChunk(ChunkDto chunk)
	{
		var dict = new Godot.Collections.Dictionary
		{
			["coord_x"] = chunk.Coord.X,
			["coord_y"] = chunk.Coord.Y
		};

		var tiles = new Godot.Collections.Array();
		foreach (var tile in chunk.Tiles)
			tiles.Add(new Godot.Collections.Dictionary
			{
				["x"] = tile.X,
				["y"] = tile.Y,
				["definition_id"] = tile.DefinitionId,
				["biome_id"] = tile.BiomeId,
				["temperature"] = tile.Temperature,
				["humidity"] = tile.Humidity
			});

		var objects = new Godot.Collections.Array();
		foreach (var obj in chunk.Objects)
			objects.Add(new Godot.Collections.Dictionary
			{
				["definition_id"] = obj.DefinitionId,
				["chunk_x"] = obj.ChunkCoord.X,
				["chunk_y"] = obj.ChunkCoord.Y,
				["tile_x"] = obj.TileCoord.X,
				["tile_y"] = obj.TileCoord.Y,
				["pos_x"] = obj.Position.X,
				["pos_y"] = obj.Position.Y,
				["pos_z"] = obj.Position.Z,
				["source"] = (int)obj.Source
			});

		dict["tiles"] = tiles;
		dict["objects"] = objects;

		return dict;
	}

	public ChunkDto DeserializeChunk(Godot.Collections.Dictionary dict)
	{
		var coord = new Vector2I(
			(int)dict["coord_x"],
			(int)dict["coord_y"]
		);

		var tiles = new List<TileInstanceDto>();
		var tileArray = (Godot.Collections.Array)dict["tiles"];
		foreach (Godot.Collections.Dictionary tileDict in tileArray)
			tiles.Add(new TileInstanceDto
			{
				X = (int)tileDict["x"],
				Y = (int)tileDict["y"],
				DefinitionId = (int)tileDict["definition_id"],
				BiomeId = (int)tileDict["biome_id"],
				Temperature = (float)tileDict["temperature"],
				Humidity = (float)tileDict["humidity"]
			});

		var objects = new List<ChunkObjectDto>();
		var objectArray = (Godot.Collections.Array)dict["objects"];
		foreach (Godot.Collections.Dictionary objDict in objectArray)
			objects.Add(new ChunkObjectDto
			{
				DefinitionId = (int)objDict["definition_id"],
				ChunkCoord = new Vector2I(
					(int)objDict["chunk_x"],
					(int)objDict["chunk_y"]
				),
				TileCoord = new Vector2I(
					(int)objDict["tile_x"],
					(int)objDict["tile_y"]
				),
				Position = new Vector3(
					(float)objDict["pos_x"],
					(float)objDict["pos_y"],
					(float)objDict["pos_z"]
				),
				Source = (ChunkObjectSource)(int)objDict["source"]
			});

		return new ChunkDto(coord, tiles, objects);
	}
}