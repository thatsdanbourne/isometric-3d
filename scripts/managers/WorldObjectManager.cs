using Godot;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public partial class WorldObjectManager : Node
{
	private int _maxSpawnsPerFrame = 6;
	private int _maxRemovesPerFrame = 10;

	private readonly Queue<Chunk> _spawnChunkQueue = new();
	private readonly Queue<ChunkObject> _activeSpawnQueue = new();
	private readonly Queue<ChunkObject> _removeQueue = new();

	private ulong _nextPickupId = 1;
	private readonly Dictionary<ulong, ItemPickup> _activePickups = new();

	private World _world;
	private RandomNumberGenerator _rng;

	private int _maxPoolSizePerType = 64;

	private PackedScene _pickupScene;


	public override void _Ready()
	{
		_world = GetParent<World>();
		_rng = new RandomNumberGenerator();
		_rng.Randomize();
		_pickupScene = ResourceLoader.Load<PackedScene>("res://scenes/ItemPickup.tscn");
	}

	public override void _Process(double delta)
	{
		ProcessSpawns();
		ProcessRemovals();
	}

	public void EnqueueChunk(Chunk chunk)
	{
		_spawnChunkQueue.Enqueue(chunk);
	}

	private void EnqueueSpawn(ChunkObject data)
	{
		if (data.RuntimeNode != null || data.MarkedForRemoval)
			return;

		_activeSpawnQueue.Enqueue(data);
	}


	public void RequestBreak(ChunkObject data)
	{
		var result = new List<ItemPickupSpawnData>();

		if (data.RuntimeNode is not { } wo)
			return;

		if (data.RuntimeNode is IItemContainer storage)
			foreach (var stack in storage.GetSlots())
			{
				if (stack is not { Count: > 0 })
					continue;

				var dir = RandomDir();
				result.Add(new ItemPickupSpawnData
				{
					PickupId = _nextPickupId++,
					ItemId = stack.Item.Id,
					Count = stack.Count,
					Position = data.Position,
					InitialVelocity = dir * ItemPickup.LaunchStrength,
					InitialVerticalVelocity = ItemPickup.BounceHeight * 8f
				});
			}

		var drops = wo.DropItems;
		if (drops is { Count: > 0 })
			foreach (var entry in drops)
			{
				if (_rng.Randf() > entry.Chance)
					continue;

				var item = ItemRegistry.GetItem(entry.ItemId);
				if (item == null)
					continue;

				var quantity = _rng.RandiRange(entry.MinQuantity, entry.MaxQuantity);

				for (var i = 0; i < quantity; i++)
				{
					var dir = RandomDir();
					result.Add(new ItemPickupSpawnData
					{
						PickupId = _nextPickupId++,
						ItemId = item.Id,
						Count = 1,
						Position = data.Position,
						InitialVelocity = dir * ItemPickup.LaunchStrength,
						InitialVerticalVelocity = ItemPickup.BounceHeight * 8f
					});
				}
			}

		var payload = BuildDropPayload(result);

		foreach (var p in payload)
			SpawnPickupFromData(p);

		_world.Sync.Rpc(nameof(_world.Sync.SpawnRemotePickups), payload);

		// clear chunk delta states
		switch (data.Source)
		{
			case ChunkObjectSource.Procedural:
				_world.MarkProceduralObjectRemoved(data.ChunkCoord, data.TileCoord);
				break;
			case ChunkObjectSource.Placed:
				_world.RemovedPlacedObject(data.ChunkCoord, data.TileCoord);
				break;
		}

		EnqueueRemoval(data);

		_world.Sync.BroadcastObjectRemoved(data.ChunkCoord, data.TileCoord);
	}

	public void ApplyRemoteBreak(Vector2I chunkCoord, Vector2I tileCoord)
	{
		if (!_world.ActiveChunks.TryGetValue(chunkCoord, out var chunk))
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

		chunk.Objects.Remove(target);
		EnqueueRemoval(target);
	}

	private Godot.Collections.Array<Godot.Collections.Dictionary> BuildDropPayload(List<ItemPickupSpawnData> data)
	{
		var result = new Godot.Collections.Array<Godot.Collections.Dictionary>();

		foreach (var d in data)
			result.Add(SerializationUtils.SerializePickup(d.PickupId, d.ItemId, d.Count, d.Position, d.InitialVelocity,
				d.InitialVerticalVelocity));

		return result;
	}

	public void HandleDropItemRequest(Player player, string itemId, int count)
	{
		if (count <= 0)
			return;

		var item = ItemRegistry.GetItem(itemId);
		if (item == null)
			return;

		if (player.DraggedStack != null)
		{
			var result = new ItemPickupSpawnData
			{
				PickupId = _nextPickupId++,
				ItemId = player.DraggedStack.Item.Id,
				Count = player.DraggedStack.Count,
				Position = player.GlobalPosition,
				InitialVelocity = RandomDir() * ItemPickup.LaunchStrength,
				InitialVerticalVelocity = ItemPickup.BounceHeight * 8f
			};

			var payload = BuildDropPayload([result]);

			foreach (var p in payload)
				SpawnPickupFromData(p);

			player.DraggedStack = null;
			_world.Sync.SyncPlayerInventoryState(player);

			_world.Sync.Rpc(nameof(_world.Sync.SpawnRemotePickups), payload);
		}
	}

	public void SpawnPickupFromData(Godot.Collections.Dictionary d)
	{
		var pickup = SerializationUtils.DeserializePickup(d);
		var item = ItemRegistry.GetItem(pickup.ItemId);
		if (item == null)
			return;

		var pickupItem = _pickupScene.Instantiate<ItemPickup>();
		pickupItem.PickupId = pickup.PickupId;
		pickupItem.Item = item;
		pickupItem.Count = pickup.Count;
		pickupItem.Position = pickup.Position;
		pickupItem.InitialVelocity = pickup.InitialVelocity;
		pickupItem.InitialVerticalVelocity = pickup.InitialVerticalVelocity;
		_world.ItemPickupContainer.AddChild(pickupItem);

		_activePickups[pickupItem.PickupId] = pickupItem;
	}

	public void HandlePickupRequest(Player player, ulong pickupId)
	{
		if (!_activePickups.TryGetValue(pickupId, out var pickup))
			return;

		if (!IsInstanceValid(pickup))
			return;

		player.GiveItem(pickup.Item, pickup.Count);
		_world.Sync.SyncPlayerInventoryState(player);
		_activePickups.Remove(pickupId);
		pickup.AnimateOut();
		_world.Sync.BroadcastPickupRemoved(pickup);
	}

	public void RemovePickupById(ulong pickupId)
	{
		if (!_activePickups.Remove(pickupId, out var pickup))
			return;

		pickup.AnimateOut();
	}

	private Vector3 RandomDir()
	{
		var dir = new Vector3(
			_rng.RandfRange(-1f, 1f),
			0f,
			_rng.RandfRange(-1f, 1f)
		);

		if (dir.LengthSquared() < 0.001f)
			dir = Vector3.Forward;

		return dir.Normalized();
	}

	public bool RequestPlace(ChunkObject data)
	{
		if (_world.ActiveChunks.TryGetValue(data.ChunkCoord, out var chunk))
		{
			chunk.Objects.Add(data);
			EnqueueSpawn(data);
		}

		_world.AddPlacedObject(data.ChunkCoord, new PlacedObjectRecord
		{
			DefinitionTypeId = data.Definition.StableId,
			TileCoord = data.TileCoord,
			Position = data.Position
		});

		_world.Sync.BroadcastObjectPlaced(data);

		return true;
	}

	public void ApplyRemotePlace(int definitionId, Vector2I chunkCoord, Vector2I tileCoord, Vector3 worldPos)
	{
		if (!_world.ActiveChunks.TryGetValue(chunkCoord, out var chunk))
			return;

		foreach (var obj in chunk.Objects)
			if (obj.TileCoord == tileCoord)
				return;

		var def = WorldObjectRegistry.GetDefinition(definitionId);
		if (def == null)
			return;

		var data = new ChunkObject
		{
			Definition = def,
			TileCoord = tileCoord,
			Position = worldPos,
			ChunkCoord = chunkCoord,
			Source = ChunkObjectSource.Placed
		};

		chunk.Objects.Add(data);
		EnqueueSpawn(data);
	}

	public void EnqueueRemoval(ChunkObject data)
	{
		data.MarkedForRemoval = true;

		if (data.RuntimeNode != null)
			_removeQueue.Enqueue(data);
	}

	private void ProcessSpawns()
	{
		var count = 0;

		if (_activeSpawnQueue.Count == 0 && _spawnChunkQueue.Count > 0)
		{
			var chunk = _spawnChunkQueue.Dequeue();

			foreach (var obj in chunk.Objects)
				_activeSpawnQueue.Enqueue(obj);
		}

		while (_activeSpawnQueue.Count > 0 && count < _maxSpawnsPerFrame)
		{
			var data = _activeSpawnQueue.Dequeue();

			if (data.MarkedForRemoval || data.RuntimeNode != null)
				continue;

			var node = WorldObjectRegistry.GetScene(data.Definition.StableId).Instantiate<WorldObject>();

			node.Data = data;
			node.World = _world;
			data.RuntimeNode = node;
			node.Initialise(data.Definition);

			EnsureParent(node, _world.WorldObjects);

			node.GlobalPosition = data.Position;
			node.Visible = true;

			if (data.Definition.BlocksTile)
				_world.BlockTile(data.TileCoord);

			// Restore state if applicable
			if (_world.ActiveChunks.TryGetValue(data.ChunkCoord, out var chunk))
				switch (node)
				{
					case IProcessingStation station:
					{
						if (chunk.StationStates.TryGetValue(data.TileCoord, out var state))
							station.BindState(state);
						break;
					}
					case IItemContainer storage:
					{
						if (chunk.StorageStates.TryGetValue(data.TileCoord, out var state))
							storage.BindState(state);
						break;
					}
				}

			count++;
		}
	}

	private void ProcessRemovals()
	{
		var count = 0;

		while (_removeQueue.Count > 0 && count < _maxRemovesPerFrame)
		{
			var data = _removeQueue.Dequeue();

			if (data.RuntimeNode == null)
				continue;

			if (data.Definition.BlocksTile)
				_world.UnblockTile(data.TileCoord);

			Recycle(data.RuntimeNode);
			data.RuntimeNode = null;
			data.MarkedForRemoval = false;

			count++;
		}
	}

	private void Recycle(WorldObject node)
	{
		node.QueueFree();
	}

	private static void EnsureParent(Node node, Node desiredParent)
	{
		if (node.GetParent() == desiredParent)
			return;

		if (node.IsInsideTree() && node.GetParent() != null)
			node.Reparent(desiredParent);
		else
			desiredParent.AddChild(node);
	}
}