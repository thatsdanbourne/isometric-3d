using Godot;
using System.Collections.Generic;

public partial class WorldObjectManager : Node
{
	private int _maxSpawnsPerFrame = 6;
	private int _maxRemovesPerFrame = 10;

	private readonly Queue<Chunk> _spawnChunkQueue = new();
	private readonly Queue<ChunkObject> _activeSpawnQueue = new();
	private readonly Queue<ChunkObject> _removeQueue = new();

	private readonly Dictionary<Vector2I, Dictionary<Vector2I, WorldObject>> _worldObjectsByChunk = new();

	private World _world;
	private RandomNumberGenerator _rng;

	private int _maxPoolSizePerType = 64;

	private PackedScene _pickupScene;


	public override void _Ready()
	{
		_world = GetParent<World>();
		_rng = new RandomNumberGenerator();
		_rng.Randomize();
		_pickupScene = ResourceLoader.Load<PackedScene>("res://scenes/objects/pickups/ItemPickup.tscn");
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

		if (data.RuntimeNode is IItemContainer &&
		    _world.TryGetChunkDelta(data.ChunkCoord, out var delta) &&
		    delta.StorageStates.TryGetValue(data.TileCoord, out var state))
			foreach (var stack in state.Slots)
			{
				if (stack is not { Count: > 0 })
					continue;

				result.Add(_world.ItemDropManager.CreatePickupData(
					stack.Item.Id,
					stack.Count,
					data.Position
				));
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
					result.Add(_world.ItemDropManager.CreatePickupData(
						item.Id,
						1,
						data.Position
					));
			}

		_world.ItemDropManager.SpawnPickups(result, true);

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

		if (!TryGetObject(chunkCoord, tileCoord, out var target))
			return;

		chunk.Objects.Remove(target.Data);
		EnqueueRemoval(target.Data);
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

		if (TryGetObject(chunkCoord, tileCoord, out _))
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
			node.Rotation = new Vector3(node.Rotation.X, data.Rotation, node.Rotation.Z);
			node.Visible = true;

			RegisterToChunkObjectMap(node);

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
			UnregisterFromChunkObjectMap(data.RuntimeNode);
			data.RuntimeNode = null;
			data.MarkedForRemoval = false;

			count++;
		}
	}

	private void RegisterToChunkObjectMap(WorldObject obj)
	{
		var chunkCoord = obj.Data.ChunkCoord;
		var tileCoord = obj.Data.TileCoord;

		if (!_worldObjectsByChunk.TryGetValue(chunkCoord, out var objectMap))
		{
			objectMap = new Dictionary<Vector2I, WorldObject>();
			_worldObjectsByChunk[chunkCoord] = objectMap;
		}

		objectMap[tileCoord] = obj;
	}

	private void UnregisterFromChunkObjectMap(WorldObject obj)
	{
		var chunkCoord = obj.Data.ChunkCoord;
		var tileCoord = obj.Data.TileCoord;

		if (!_worldObjectsByChunk.TryGetValue(chunkCoord, out var objectMap))
			return;

		objectMap.Remove(tileCoord);

		if (objectMap.Count == 0)
			_worldObjectsByChunk.Remove(chunkCoord);
	}

	public bool TryGetObject(Vector2I chunkCoord, Vector2I tileCoord, out WorldObject obj)
	{
		obj = null;

		return _worldObjectsByChunk.TryGetValue(chunkCoord, out var objectMap)
		       && objectMap.TryGetValue(tileCoord, out obj);
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