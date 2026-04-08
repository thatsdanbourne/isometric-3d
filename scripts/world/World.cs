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
			var player = GameManager.Instance.LocalPlayer;
			if (player == null || !IsInstanceValid(player))
				return false;

			return TryPlaceItemAuthoritative(player, item, def.StableId, tile, chunkCoord, worldPos);
		}

		RpcId(1, nameof(RequestPlaceObject), item.Id, def.StableId, chunkCoord, tile, worldPos);
		return true;
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	private void RequestPlaceObject(string itemId, int defId, Vector2I chunkCoord, Vector2I tileCoord, Vector3 worldPos)
	{
		if (!Multiplayer.IsServer())
			return;

		if (!ActiveChunks.ContainsKey(chunkCoord))
			return;

		var peerId = Multiplayer.GetRemoteSenderId();
		var player = GetPlayerById(peerId);
		if (player == null)
			return;

		var item = ItemRegistry.GetItem(itemId) as PlaceableItem;
		if (item == null)
			return;

		var def = item.PlaceableObjectDefinition;
		if (def == null)
			return;

		TryPlaceItemAuthoritative(player, item, defId, tileCoord, chunkCoord, worldPos);
	}

	private bool TryPlaceItemAuthoritative(
		Player player,
		PlaceableItem item,
		int defId,
		Vector2I tileCoord,
		Vector2I chunkCoord,
		Vector3 worldPos)
	{
		if (!ActiveChunks.ContainsKey(chunkCoord))
		{
			GD.PrintErr($"Tried to place item in unloaded chunk {chunkCoord}");
			return false;
		}

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

		SyncPlayerInventoryState(player);
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

	// station sync
	public void BroadcastStorageState(StorageStateData state)
	{
		if (!Multiplayer.IsServer())
			return;

		var chunkCoord = TileUtils.WorldToChunk(TileUtils.TileToWorld(state.TileCoord));
		var serialised = SerializationUtils.SerializeStorageState(state);

		foreach (var peerId in ChunkManager.GetPeersInterestedInChunk(chunkCoord))
			RpcId(peerId, nameof(ReceiveStorageState), serialised);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void ReceiveStorageState(Godot.Collections.Dictionary stateData)
	{
		var state = SerializationUtils.DeserializeStorageState(stateData);
		WorldObjectManager.ApplyRemoteStorageState(state);
	}

	public void SyncPlayerInventoryState(Player player)
	{
		if (!Multiplayer.IsServer())
			return;

		var data = SerializationUtils.SerializePlayerInventoryState(player);
		var localPeerId = Multiplayer.GetUniqueId();

		if (player.PlayerId == localPeerId)
		{
			ApplyPlayerInventoryStateLocally(data);
			return;
		}

		RpcId(player.PlayerId, nameof(ReceivePlayerInventoryState), data);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void ReceivePlayerInventoryState(Godot.Collections.Dictionary data)
	{
		ApplyPlayerInventoryStateLocally(data);
	}

	private IItemContainer ResolveStorageContainer(Vector2I storageTileCoord)
	{
		var chunkCoord = TileUtils.WorldToChunk(TileUtils.TileToWorld(storageTileCoord));

		if (!ActiveChunks.TryGetValue(chunkCoord, out var chunk))
			return null;

		foreach (var obj in chunk.Objects)
		{
			if (obj.TileCoord != storageTileCoord)
				continue;

			if (obj.RuntimeNode is IItemContainer storage)
				return storage;
		}

		return null;
	}

	private IItemContainer ResolveContainer(Player player, ContainerKind kind, Vector2I storageTileCoord)
	{
		return kind switch
		{
			ContainerKind.Inventory => player.Inventory,
			ContainerKind.Hotbar => player.Hotbar,
			ContainerKind.Storage => ResolveStorageContainer(storageTileCoord),
			_ => null
		};
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	private void RequestContainerClick(
		int containerKind,
		int storageTileX,
		int storageTileY,
		int slotIndex,
		int mouseButton,
		bool shiftHeld)
	{
		if (!Multiplayer.IsServer())
			return;

		var peerId = Multiplayer.GetRemoteSenderId();
		var player = GetPlayerById(peerId);
		if (player == null)
			return;

		var kind = (ContainerKind)containerKind;
		var storageTileCoord = new Vector2I(storageTileX, storageTileY);

		HandleContainerClickRequest(player, kind, storageTileCoord, slotIndex, mouseButton, shiftHeld);
	}

	private void HandleContainerClickRequest(
		Player player,
		ContainerKind kind,
		Vector2I storageTileCoord,
		int slotIndex,
		int mouseButton,
		bool shiftHeld
	)
	{
		var container = ResolveContainer(player, kind, storageTileCoord);
		if (container == null)
			return;

		var storageContainer = kind == ContainerKind.Storage ? container : null;

		if (slotIndex < 0 || slotIndex >= container.SlotCount)
			return;

		if (shiftHeld)
		{
			switch (kind)
			{
				case ContainerKind.Inventory:
					player.DraggedStack = InventoryManager.Instance.ShiftClick(
						container,
						slotIndex,
						player.Hotbar
					);
					break;

				case ContainerKind.Hotbar:
					player.DraggedStack = InventoryManager.Instance.ShiftClick(
						container,
						slotIndex,
						player.Inventory
					);
					break;

				case ContainerKind.Storage:
					player.DraggedStack = InventoryManager.Instance.ShiftClick(
						container,
						slotIndex,
						player.Hotbar,
						player.Inventory
					);
					break;
			}
		}
		else
		{
			if (mouseButton == 0)
				player.DraggedStack = InventoryManager.Instance.LeftClick(
					container,
					slotIndex,
					player.DraggedStack
				);
			else
				player.DraggedStack = InventoryManager.Instance.RightClick(
					container,
					slotIndex,
					player.DraggedStack
				);
		}

		SyncPlayerInventoryState(player);

		if (storageContainer is IChunkStateful<StorageStateData> storage)
		{
			var chunkCoord = TileUtils.WorldToChunk(TileUtils.TileToWorld(storageTileCoord));
			var delta = GetOrCreateChunkDelta(chunkCoord);
			var newState = storage.CaptureState();
			delta.StorageStates[storageTileCoord] = newState;

			if (ActiveChunks.TryGetValue(chunkCoord, out var chunk))
				chunk.StorageStates[storageTileCoord] = newState;

			ChunkManager.InvalidateServerChunk(chunkCoord);

			BroadcastStorageState(delta.StorageStates[storageTileCoord]);
		}
	}

	public void HandleContainerClick(
		ContainerKind kind,
		Vector2I storageTileCoord,
		int slotIndex,
		int mouseButton,
		bool shiftHeld)
	{
		var isServerAuthority = !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer();
		var localPlayer = GameManager.Instance.LocalPlayer;

		if (localPlayer == null || !IsInstanceValid(localPlayer))
			return;

		if (isServerAuthority)
		{
			HandleContainerClickRequest(localPlayer, kind, storageTileCoord, slotIndex, mouseButton, shiftHeld);
			localPlayer.HUD.RefreshUI();
			return;
		}

		RpcId(
			1,
			nameof(RequestContainerClick),
			(int)kind,
			storageTileCoord.X,
			storageTileCoord.Y,
			slotIndex,
			mouseButton,
			shiftHeld
		);
	}

	private void ApplyPlayerInventoryStateLocally(Godot.Collections.Dictionary data)
	{
		var player = GameManager.Instance.LocalPlayer;
		if (player == null || !IsInstanceValid(player))
			return;

		var state = SerializationUtils.DeserializePlayerInventoryState(data);

		for (var i = 0; i < player.Inventory.SlotCount; i++)
			player.Inventory.SetSlot(i, i < state.Inventory.Length ? state.Inventory[i] : null);

		for (var i = 0; i < player.Hotbar.SlotCount; i++)
			player.Hotbar.SetSlot(i, i < state.Hotbar.Length ? state.Hotbar[i] : null);

		player.DraggedStack = state.DraggedStack;

		player.HUD.RefreshUI();
		player.HUD.UpdateDraggedCursorFromPlayerState();
	}

	// object break/place

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

	// chunk streaming

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
		var serialized = SerializationUtils.SerializeChunk(chunk);
		RpcId(peerId, nameof(ReceiveChunk), serialized);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	public void ReceiveChunk(Godot.Collections.Dictionary chunkData)
	{
		var chunk = SerializationUtils.DeserializeChunk(chunkData);
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