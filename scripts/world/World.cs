using Godot;
using System;
using System.Collections.Generic;
using System.Data;

public partial class World : Node3D
{
	[Export] public Node3D WorldObjects;
	[Export] public Node3D WorldMobs;
	[Export] public GridMap GroundMap;
	[Export] public GridMap WaterMap;
	[Export] public Node3D PlayerContainer;
	[Export] public DayNightCycle DayNightCycle;
	[Export] public WeatherManager WeatherManager;
	[Export] public Node3D ItemPickupContainer;

	private readonly List<Player> _players = new();
	public IReadOnlyList<Player> Players => _players;

	public double WorldTimeSeconds;

	public int ChunkSize = TileUtils.ChunkSize;
	public int ChunkRadius = 4;

	public FastNoiseLite TempNoise;
	public FastNoiseLite HumidityNoise;
	public FastNoiseLite RiverNoise;
	public FastNoiseLite DrainageNoise;
	public FastNoiseLite LakeNoise;
	public FastNoiseLite BankNoise;

	public readonly Dictionary<Vector2I, Chunk> ServerChunks = new();
	public readonly Dictionary<Vector2I, Chunk> ActiveChunks = new();
	public readonly Dictionary<Vector2I, ChunkDeltaData> ChunkDeltas = new();
	private readonly Dictionary<Vector2I, int> _blockedTiles = new();

	public int TerrainSeed;
	public Vector2I WorldOffset; // prevents sampling noise at (0,0)

	private RandomNumberGenerator _rng;

	public ChunkManager ChunkManager { get; private set; }
	public ChunkGenerator ChunkGenerator { get; private set; }

	public WorldObjectManager WorldObjectManager { get; private set; }

	public MobStreamer MobStreamer;
	public BiomeSampler BiomeSampler;

	private Vector2I? _lastSubmittedChunkCenter;

	private bool _worldReady;


	public void InitialiseWorld(int terrainSeed)
	{
		_rng = new RandomNumberGenerator();
		_rng.Randomize();

		TerrainSeed = terrainSeed;
		SetupNoise();
		RuleRegistry.LoadAll(TerrainSeed, WorldOffset);

		// WorldOffset = new Vector2I(
		// 	(int)_rng.Randi() % 100000,
		// 	(int)_rng.Randi() % 100000
		// );

		WorldOffset = new Vector2I(0, 0);

		ChunkManager = new ChunkManager(this, ChunkSize, ChunkRadius);
		WorldObjectManager = GetNode<WorldObjectManager>("World/WorldObjectManager");
		MobStreamer = GetNode<MobStreamer>("World/MobStreamer");

		BiomeSampler = new BiomeSampler(TempNoise, HumidityNoise, RiverNoise, LakeNoise, DrainageNoise, BankNoise,
			WorldOffset);

		var debugTeleporter = GetNode<DebugBiomeTeleporter>("World/DebugBiomeTeleporter");
		debugTeleporter.World = this;

		ChunkGenerator = new ChunkGenerator(this, TerrainSeed);
		ChunkGenerator.Start();
		_worldReady = true;
	}

	public override void _ExitTree()
	{
		ChunkGenerator?.Stop();
	}

	public void AddPlayer(Player player, Vector3 spawnPosition)
	{
		if (player == null) return;

		if (_players.Contains(player)) return;
		player.Position = spawnPosition;
		PlayerContainer.AddChild(player);
		_players.Add(player);
	}

	public void RemovePlayer(Player player)
	{
		if (player == null) return;

		player.QueueFree();
		_players.Remove(player);
		ChunkManager.ForgetPeerChunks(player.PlayerId);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!_worldReady)
			return;

		var isMultiplayer = Multiplayer.HasMultiplayerPeer();
		var isServer = !isMultiplayer || Multiplayer.IsServer();

		if (isServer)
		{
			var playerPositions = new List<Vector3>();
			foreach (var player in _players)
			{
				if (player == null || !IsInstanceValid(player) || !player.IsInsideTree())
					continue;

				playerPositions.Add(player.GlobalPosition);
			}

			ChunkManager.UpdateServerChunkCache(playerPositions);
			ChunkGenerator.ProcessBuiltChunks();
		}

		var localPlayer = GameManager.Instance.LocalPlayer;
		if (localPlayer != null && IsInstanceValid(localPlayer) && localPlayer.IsInsideTree())
			if (isServer)
			{
				// single player or host
				// activate local chunks directly from server-size cache
				ChunkManager.UpdateLocalChunks(localPlayer.GlobalPosition);
			}
			else
			{
				// client
				// request desired chunks from server and finalise received chunks
				UpdateLocalChunkInterest();
				ChunkGenerator.ProcessClientChunkQueue();
			}

		WorldTimeSeconds += delta;
	}

	private void SetupNoise()
	{
		TempNoise = new FastNoiseLite
		{
			Seed = TerrainSeed + 1000,
			NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
			Frequency = 0.0015f,
			FractalOctaves = 3,
			FractalGain = 0.5f,
			FractalLacunarity = 2.0f
		};

		HumidityNoise = new FastNoiseLite
		{
			Seed = TerrainSeed + 2000,
			NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
			Frequency = 0.003f,
			FractalOctaves = 4,
			FractalGain = 0.55f,
			FractalLacunarity = 2.0f
		};

		RiverNoise = new FastNoiseLite
		{
			Seed = TerrainSeed + 3000,
			NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
			Frequency = 0.0025f,
			FractalOctaves = 3,
			FractalGain = 0.5f,
			FractalLacunarity = 2f
		};

		LakeNoise = new FastNoiseLite
		{
			Seed = TerrainSeed + 4000,
			NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
			Frequency = 0.01f,
			FractalOctaves = 3,
			FractalGain = 0.5f,
			FractalLacunarity = 2.0f
		};

		DrainageNoise = new FastNoiseLite
		{
			Seed = TerrainSeed + 5000,
			NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
			Frequency = 0.001f,
			FractalOctaves = 2,
			FractalGain = 0.5f,
			FractalLacunarity = 2.0f
		};

		BankNoise = new FastNoiseLite
		{
			Seed = TerrainSeed + 6000,
			NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
			Frequency = 0.025f,
			FractalOctaves = 2,
			FractalGain = 0.5f,
			FractalLacunarity = 2.0f
		};
	}

	public void BlockTile(Vector2I tile)
	{
		if (_blockedTiles.TryGetValue(tile, out var count))
			_blockedTiles[tile] = count + 1;
		else
			_blockedTiles[tile] = 1;
	}

	public void UnblockTile(Vector2I tile)
	{
		if (!_blockedTiles.TryGetValue(tile, out var count)) return;

		count--;

		if (count <= 0)
			_blockedTiles.Remove(tile);
		else
			_blockedTiles[tile] = count;
	}

	public bool CanPlace(Vector2I tile, PlaceableItem item)
	{
		if (_blockedTiles.ContainsKey(tile)) return false;

		return true;
	}

	public bool PlaceItem(Vector2I tile, PlaceableItem item)
	{
		var worldPos = TileUtils.TileToWorld(tile);
		var chunkCoord = TileUtils.WorldToChunk(worldPos);

		var def = item.PlaceableObjectDefinition;

		var isServerAuthority = !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer();

		if (isServerAuthority)
		{
			if (!ActiveChunks.ContainsKey(chunkCoord))
			{
				GD.PrintErr($"Tried to place item in unloaded chunk {chunkCoord}");
				return false;
			}

			var chunkObj = new ChunkObject
			{
				Definition = def,
				TileCoord = tile,
				Position = worldPos,
				ChunkCoord = chunkCoord,
				Source = ChunkObjectSource.Placed
			};

			return WorldObjectManager.RequestPlace(chunkObj);
		}

		RpcId(1, nameof(RequestPlaceObject), def.StableId, chunkCoord, tile, worldPos);
		return true;
	}

	public void TryBreakObject(ChunkObject data)
	{
		var isServerAuthority = !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer();

		if (isServerAuthority)
		{
			WorldObjectManager.RequestBreak(data);
			return;
		}

		RpcId(1, nameof(RequestBreakObject), data.ChunkCoord, data.TileCoord);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	private void RequestBreakObject(Vector2I chunkCoord, Vector2I tileCoord)
	{
		if (!Multiplayer.IsServer())
			return;

		if (!ActiveChunks.TryGetValue(chunkCoord, out var chunk))
			return;

		ChunkObject target = null;

		foreach (var obj in chunk.Objects)
			if (obj.TileCoord == tileCoord)
			{
				target = obj;
				break;
			}

		if (target == null)
			return;

		WorldObjectManager.RequestBreak(target);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	private void RequestPlaceObject(int defId, Vector2I chunkCoord, Vector2I tileCoord, Vector3 worldPos)
	{
		if (!Multiplayer.IsServer())
			return;

		if (!ActiveChunks.ContainsKey(chunkCoord))
			return;

		var def = WorldObjectRegistry.GetDefinition(defId);
		if (def == null)
			return;

		var chunkObj = new ChunkObject
		{
			Definition = def,
			TileCoord = tileCoord,
			Position = worldPos,
			ChunkCoord = chunkCoord,
			Source = ChunkObjectSource.Placed
		};

		WorldObjectManager.RequestPlace(chunkObj);
	}

	public bool TryGetChunkDelta(Vector2I chunkCoord, out ChunkDeltaData delta)
	{
		return ChunkDeltas.TryGetValue(chunkCoord, out delta);
	}

	public ChunkDeltaData GetOrCreateChunkDelta(Vector2I chunkCoord)
	{
		if (ChunkDeltas.TryGetValue(chunkCoord, out var delta)) return delta;
		delta = new ChunkDeltaData();
		ChunkDeltas[chunkCoord] = delta;

		return delta;
	}

	public ChunkDeltaData MutateChunkDelta(Vector2I coord)
	{
		var delta = GetOrCreateChunkDelta(coord);
		ChunkManager.InvalidateServerChunk(coord);
		return delta;
	}

	public void MarkProceduralObjectRemoved(Vector2I chunkCoord, Vector2I tileCoord)
	{
		var delta = MutateChunkDelta(chunkCoord);
		delta.RemovedProceduralObjects.Add(tileCoord);
	}

	public void RemovedPlacedObject(Vector2I chunkCoord, Vector2I tileCoord)
	{
		var delta = MutateChunkDelta(chunkCoord);
		delta.PlacedObjectsByTile.Remove(tileCoord);
		delta.StorageStates.Remove(tileCoord);
		delta.StationStates.Remove(tileCoord);
	}

	public void AddPlacedObject(Vector2I chunkCoord, PlacedObjectRecord record)
	{
		var delta = MutateChunkDelta(chunkCoord);
		delta.PlacedObjectsByTile[record.TileCoord] = record;
	}

	public BiomeId GetBiomeAtPos(Vector3 worldPos)
	{
		var tile = TileUtils.WorldToTile(worldPos);
		var chunkCoord = TileUtils.WorldToChunk(worldPos);

		if (!ActiveChunks.TryGetValue(chunkCoord, out var chunk))
			return BiomeId.Unknown;

		var localX = tile.X - chunkCoord.X * ChunkSize;
		var localY = tile.Y - chunkCoord.Y * ChunkSize;

		return chunk.Tiles[localX, localY].Biome;
	}

	public TileInstance? GetTileAtPos(Vector3 worldPos)
	{
		var tile = TileUtils.WorldToTile(worldPos);
		var chunkCoord = TileUtils.WorldToChunk(worldPos);

		if (!ActiveChunks.TryGetValue(chunkCoord, out var chunk)) return null;

		var localX = tile.X - chunkCoord.X * ChunkSize;
		var localY = tile.Y - chunkCoord.Y * ChunkSize;

		return chunk.Tiles[localX, localY];
	}

	public bool IsTileBlocked(Vector2I tile)
	{
		return _blockedTiles.ContainsKey(tile);
	}

	public Player GetNearestPlayer(Vector3 worldPos, float maxDistance)
	{
		Player best = null;
		var bestDistSq = maxDistance * maxDistance;

		foreach (var player in _players)
		{
			if (player == null || !IsInstanceValid(player) || !player.IsInsideTree())
				continue;

			var distSq = worldPos.DistanceSquaredTo(player.GlobalPosition);
			if (distSq > bestDistSq)
				continue;

			best = player;
			bestDistSq = distSq;
		}

		return best;
	}

	public bool HasPlayer(int playerId)
	{
		foreach (var player in Players)
			if (player != null && player.PlayerId == playerId)
				return true;

		return false;
	}

	public Player GetPlayerById(int playerId)
	{
		foreach (var player in Players)
			if (player != null && player.PlayerId == playerId)
				return player;

		return null;
	}

	public void BroadcastObjectRemoved(Vector2I chunkCoord, Vector2I tileCoord)
	{
		if (!Multiplayer.IsServer())
			return;

		foreach (var peerId in ChunkManager.GetPeersInterestedInChunk(chunkCoord))
			RpcId(peerId, nameof(ReceiveObjectRemoved), chunkCoord, tileCoord);
	}

	public void BroadcastObjectPlaced(ChunkObject data)
	{
		if (!Multiplayer.IsServer())
			return;

		foreach (var peerId in ChunkManager.GetPeersInterestedInChunk(data.ChunkCoord))
			RpcId(
				peerId,
				nameof(ReceiveObjectPlaced),
				data.Definition.StableId,
				data.ChunkCoord,
				data.TileCoord,
				data.Position);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void ReceiveObjectRemoved(Vector2I chunkCoord, Vector2I tileCoord)
	{
		WorldObjectManager.ApplyRemoteBreak(chunkCoord, tileCoord);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void ReceiveObjectPlaced(int defId, Vector2I chunkCoord, Vector2I tileCoord, Vector3 worldPos)
	{
		WorldObjectManager.ApplyRemotePlace(defId, chunkCoord, tileCoord, worldPos);
	}

	private void UpdateLocalChunkInterest()
	{
		var localPlayer = GameManager.Instance.LocalPlayer;
		if (localPlayer == null || !IsInstanceValid(localPlayer) || !localPlayer.IsInsideTree())
			return;

		var center = TileUtils.WorldToChunk(localPlayer.GlobalPosition);

		if (_lastSubmittedChunkCenter.HasValue && _lastSubmittedChunkCenter.Value == center)
			return;

		_lastSubmittedChunkCenter = center;

		var coords = BuildDesiredChunkArray(center);

		if (Multiplayer.HasMultiplayerPeer() && !Multiplayer.IsServer()) RpcId(1, nameof(SubmitDesiredChunks), coords);
	}

	private Godot.Collections.Array<Vector2I> BuildDesiredChunkArray(Vector2I center)
	{
		var coords = new Godot.Collections.Array<Vector2I>();

		for (var x = -ChunkRadius; x <= ChunkRadius; x++)
		for (var y = -ChunkRadius; y <= ChunkRadius; y++)
			coords.Add(new Vector2I(center.X + x, center.Y + y));

		return coords;
	}

	public void SendChunkUnloadToPeer(int peerId, Vector2I chunkCoord)
	{
		var localPeerId = Multiplayer.GetUniqueId();

		if (peerId == localPeerId)
		{
			ChunkManager.RemoveChunk(chunkCoord);
			return;
		}

		RpcId(peerId, nameof(ReceiveChunkUnload), chunkCoord);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void ReceiveChunkUnload(Vector2I chunkCoord)
	{
		if (!ActiveChunks.ContainsKey(chunkCoord))
			return;

		ChunkManager.RemoveChunk(chunkCoord);
	}

	public void SendChunkToPeer(int peerId, ChunkDto chunk)
	{
		var serialized = ChunkManager.SerializeChunk(chunk);
		RpcId(peerId, nameof(ReceiveChunk), serialized);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	public void ReceiveChunk(Godot.Collections.Dictionary chunkData)
	{
		var chunk = ChunkManager.DeserializeChunk(chunkData);
		ChunkGenerator.EnqueueClientChunk(chunk);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	private void SubmitDesiredChunks(Godot.Collections.Array<Vector2I> coords)
	{
		if (!Multiplayer.IsServer())
			return;

		var peerId = Multiplayer.GetRemoteSenderId();
		ChunkManager.UpdatePeerInterest(peerId, coords);
	}
}