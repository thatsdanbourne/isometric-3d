using System;
using Godot;
using System.Collections.Generic;
using System.Diagnostics.SymbolStore;
using System.Threading.Tasks;

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
	private float _worldTimeSyncInterval = 10f;
	private float _worldTimeSyncTimer;

	public int ChunkSize = TileUtils.ChunkSize;
	private int _chunkRadius = 4;

	private FastNoiseLite _tempNoise;
	private FastNoiseLite _humidityNoise;
	private FastNoiseLite _riverNoise;
	private FastNoiseLite _drainageNoise;
	private FastNoiseLite _lakeNoise;
	private FastNoiseLite _bankNoise;

	public readonly Dictionary<Vector2I, Chunk> ServerChunks = new();
	public readonly Dictionary<Vector2I, Chunk> ActiveChunks = new();
	public readonly Dictionary<Vector2I, ChunkDeltaData> ChunkDeltas = new();
	private readonly Dictionary<Vector2I, int> _blockedTiles = new();

	public int TerrainSeed;
	public Vector2I WorldOffset; // prevents sampling noise at (0,0)

	private RandomNumberGenerator _rng;

	public WorldSync Sync { get; private set; }
	public ChunkManager ChunkManager { get; private set; }
	public ChunkGenerator ChunkGenerator { get; private set; }

	public WorldObjectManager WorldObjectManager { get; private set; }

	public MobStreamer MobStreamer;
	public BiomeSampler BiomeSampler;

	private bool _worldReady;


	public void InitialiseWorld(int terrainSeed, double worldTimeSeconds = 0)
	{
		_rng = new RandomNumberGenerator();
		_rng.Randomize();
		WorldTimeSeconds = worldTimeSeconds;
		TerrainSeed = terrainSeed;
		SetupNoise();
		RuleRegistry.LoadAll(TerrainSeed, WorldOffset);

		// WorldOffset = new Vector2I(
		// 	(int)_rng.Randi() % 100000,
		// 	(int)_rng.Randi() % 100000
		// );

		WorldOffset = new Vector2I(0, 0);

		ChunkManager = new ChunkManager(this, ChunkSize, _chunkRadius);
		WorldObjectManager = GetNode<WorldObjectManager>("WorldObjectManager");
		MobStreamer = GetNode<MobStreamer>("MobStreamer");

		BiomeSampler = new BiomeSampler(_tempNoise, _humidityNoise, _riverNoise, _lakeNoise, _drainageNoise, _bankNoise,
			WorldOffset);

		var debugTeleporter = GetNode<DebugBiomeTeleporter>("DebugBiomeTeleporter");
		debugTeleporter.World = this;

		ChunkGenerator = new ChunkGenerator(this, TerrainSeed);
		ChunkGenerator.Start();
		_worldReady = true;
	}

	public override void _Ready()
	{
		if (Multiplayer.IsServer())
			WorldTimeSeconds = DayNightCycle.DayLength * 0.2;

		Sync = new WorldSync();
		Sync.Name = "WorldSync";
		AddChild(Sync);
		Sync.Init(this);
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

		WorldTimeSeconds += delta;

		var isMultiplayer = Multiplayer.HasMultiplayerPeer();
		var isServer = !isMultiplayer || Multiplayer.IsServer();

		if (isServer)
		{
			_worldTimeSyncTimer += (float)delta;

			if (_worldTimeSyncTimer >= _worldTimeSyncInterval)
			{
				_worldTimeSyncTimer = 0f;
				Sync.BroadcastWorldTime(WorldTimeSeconds);
			}

			var playerPositions = new List<Vector3>();
			foreach (var player in _players)
			{
				if (player == null || !IsInstanceValid(player) || !player.IsInsideTree())
					continue;

				playerPositions.Add(player.GlobalPosition);
			}

			ChunkManager.UpdateServerChunkCache(playerPositions);
			ChunkGenerator.ProcessBuiltChunks();
			ChunkManager.UpdateAuthorityChunks(playerPositions);

			MobStreamer.Tick(delta);

			foreach (var mob in MobStreamer.GetActiveMobs())
			{
				if (mob == null || !IsInstanceValid(mob))
					continue;

				mob.TickAI(delta);
			}
		}

		var localPlayer = GameManager.Instance.LocalPlayer;
		if (localPlayer != null && IsInstanceValid(localPlayer) && localPlayer.IsInsideTree())
			if (!isServer)
			{
				// client
				// request desired chunks from server and finalise received chunks
				Sync.UpdateLocalChunkInterest(localPlayer.GlobalPosition, _chunkRadius);
				ChunkGenerator.ProcessClientChunkQueue();
			}
	}

	private void SetupNoise()
	{
		_tempNoise = new FastNoiseLite
		{
			Seed = TerrainSeed + 1000,
			NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
			Frequency = 0.0015f,
			FractalOctaves = 3,
			FractalGain = 0.5f,
			FractalLacunarity = 2.0f
		};

		_humidityNoise = new FastNoiseLite
		{
			Seed = TerrainSeed + 2000,
			NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
			Frequency = 0.003f,
			FractalOctaves = 4,
			FractalGain = 0.55f,
			FractalLacunarity = 2.0f
		};

		_riverNoise = new FastNoiseLite
		{
			Seed = TerrainSeed + 3000,
			NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
			Frequency = 0.0025f,
			FractalOctaves = 3,
			FractalGain = 0.5f,
			FractalLacunarity = 2f
		};

		_lakeNoise = new FastNoiseLite
		{
			Seed = TerrainSeed + 4000,
			NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
			Frequency = 0.01f,
			FractalOctaves = 3,
			FractalGain = 0.5f,
			FractalLacunarity = 2.0f
		};

		_drainageNoise = new FastNoiseLite
		{
			Seed = TerrainSeed + 5000,
			NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
			Frequency = 0.001f,
			FractalOctaves = 2,
			FractalGain = 0.5f,
			FractalLacunarity = 2.0f
		};

		_bankNoise = new FastNoiseLite
		{
			Seed = TerrainSeed + 6000,
			NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
			Frequency = 0.025f,
			FractalOctaves = 2,
			FractalGain = 0.5f,
			FractalLacunarity = 2.0f
		};
	}

	public void CorrectWorldTime(double serverTime)
	{
		WorldTimeSeconds = serverTime;
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

		Sync.RpcId(1, nameof(WorldSync.RequestPlaceObject), item.Id, def.StableId, chunkCoord, tile, worldPos);
		return true;
	}

	public bool TryPlaceItem(
		Player player,
		PlaceableItem item,
		int defId,
		Vector2I tileCoord,
		Vector2I chunkCoord,
		Vector3 worldPos)
	{
		// Remove one item from the authoritative player inventory first
		var remaining = InventoryManager.Instance.RemoveItem(player, item, 1);
		if (remaining > 0)
		{
			GD.PrintErr($"Player {player.PlayerId} tried to place {item.DisplayName} without having it.");
			return false;
		}

		var def = WorldObjectRegistry.GetDefinition(defId);
		if (def == null)
		{
			InventoryManager.Instance.AddItem(player, item, 1);
			return false;
		}

		var chunkObj = new ChunkObject
		{
			Definition = def,
			TileCoord = tileCoord,
			Position = worldPos,
			ChunkCoord = chunkCoord,
			Source = ChunkObjectSource.Placed
		};

		if (!WorldObjectManager.RequestPlace(chunkObj))
		{
			// refund if placement failed
			InventoryManager.Instance.AddItem(player, item, 1);
			return false;
		}

		Sync.SyncPlayerInventoryState(player);
		return true;
	}

	public void TryBreakObject(ChunkObject data)
	{
		Sync.HandleBreakObject(data.ChunkCoord, data.TileCoord);
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

	private bool TryResolveWorldObjectFromChunkMap(Dictionary<Vector2I, Chunk> chunks, Vector2I tileCoord,
		Vector2I chunkCoord, out WorldObject worldObject)
	{
		worldObject = null;

		if (!chunks.TryGetValue(chunkCoord, out var chunk))
			return false;

		foreach (var obj in chunk.Objects)
		{
			if (obj.TileCoord != tileCoord)
				continue;

			worldObject = obj.RuntimeNode;
			return worldObject != null;
		}

		return false;
	}

	public ChunkObject ResolveChunkObject(Vector2I tileCoord)
	{
		var chunkCoord = TileUtils.WorldToChunk(TileUtils.TileToWorld(tileCoord));

		if (ActiveChunks.TryGetValue(chunkCoord, out var activeChunk))
			foreach (var obj in activeChunk.Objects)
				if (obj.TileCoord == tileCoord)
					return obj;

		if (ServerChunks.TryGetValue(chunkCoord, out var serverChunk))
			foreach (var obj in serverChunk.Objects)
				if (obj.TileCoord == tileCoord)
					return obj;

		return null;
	}

	public WorldObject ResolveWorldObject(Vector2I tileCoord)
	{
		var chunkCoord = TileUtils.WorldToChunk(TileUtils.TileToWorld(tileCoord));

		if (TryResolveWorldObjectFromChunkMap(ActiveChunks, tileCoord, chunkCoord, out var worldObject))
			return worldObject;

		if (TryResolveWorldObjectFromChunkMap(ServerChunks, tileCoord, chunkCoord, out worldObject))
			return worldObject;

		return null;
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

	public void HandleUseActiveToolRequest(Player player, Vector3 swingDir)
	{
		if (player == null || !IsInstanceValid(player))
			return;

		var tool = player.GetActiveTool();
		if (tool == null)
			return;

		if (swingDir.LengthSquared() < 0.001f)
			return;

		swingDir = swingDir.Normalized();

		player.StartSwingCooldown(tool);

		Sync.Rpc(nameof(WorldSync.PlayRemoteUseActiveToolVisual), player.PlayerId, tool.Id, swingDir);

		ResolveMeleeHit(
			player,
			tool,
			swingDir,
			player.ToolQuery,
			hitResult =>
			{
				switch (hitResult.Outcome)
				{
					case ToolHitOutcome.Failed:
						Sync.SendAttackFeedback(player.PlayerId, (int)ToolHitOutcome.Failed);
						break;
					case ToolHitOutcome.Destroyed:
						Sync.SendAttackFeedback(player.PlayerId, (int)ToolHitOutcome.Destroyed);
						break;
				}
			});
	}

	public void ResolveEnemyMeleeAttack(Mob attacker, ToolItem tool, Vector3 swingDir,
		PhysicsRayQueryParameters3D toolQuery)
	{
		if (attacker == null || !IsInstanceValid(attacker))
			return;

		ResolveMeleeHit(
			attacker,
			tool,
			swingDir,
			toolQuery,
			hitResult => { });
	}

	private async void ResolveMeleeHit(Node3D attacker, ToolItem tool, Vector3 swingDir,
		PhysicsRayQueryParameters3D toolQuery, Action<ToolHitResult> onResult)
	{
		if (attacker == null || !IsInstanceValid(attacker))
			return;

		if (tool == null)
			return;

		if (swingDir.LengthSquared() < 0.001f)
			return;

		swingDir = swingDir.Normalized();

		await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);

		var space = attacker.GetWorld3D().DirectSpaceState;
		var hitResult = CombatUtils.PerformMeleeHit(attacker, tool, swingDir, space, toolQuery);

		onResult?.Invoke(hitResult);
	}
}