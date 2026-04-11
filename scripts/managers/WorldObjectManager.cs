using Godot;
using System.Collections.Generic;

public partial class WorldObjectManager : Node
{
	private int _maxSpawnsPerFrame = 6;
	private int _maxRemovesPerFrame = 10;

	private readonly Queue<Chunk> _spawnChunkQueue = new();
	private readonly Queue<ChunkObject> _activeSpawnQueue = new();
	private readonly Queue<ChunkObject> _removeQueue = new();

	// private readonly Dictionary<string, Stack<WorldObject>> _pools = new();

	private World _world;
	private RandomNumberGenerator _rng;

	private int _maxPoolSizePerType = 64;

	private PackedScene _pickupScene;


	public override void _Ready()
	{
		_world = GetParent().GetParent<World>();
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
		if (data.RuntimeNode is IItemContainer storage)
			foreach (var stack in storage.GetSlots())
			{
				if (stack is not { Count: > 0 }) continue;

				var pickup = _pickupScene.Instantiate<ItemPickup>();
				pickup.Item = stack.Item;
				pickup.Count = stack.Count;

				_world.ItemPickupContainer.AddChild(pickup);
				pickup.GlobalPosition = data.Position;
			}

		SpawnDrops(data);

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

		if (_world.Multiplayer.IsServer())
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

	private void SpawnDrops(ChunkObject data)
	{
		if (data.RuntimeNode is not { } wo)
			return;

		var drops = wo.DropItems;
		if (drops == null || drops.Count == 0) return;

		foreach (var entry in drops)
		{
			if (_rng.Randf() > entry.Chance)
				continue;

			var item = ItemRegistry.GetItem(entry.ItemId);
			if (item == null) continue;

			var quantity = _rng.RandiRange(entry.MinQuantity, entry.MaxQuantity);

			for (var n = 0; n < quantity; n++)
			{
				var pickup = _pickupScene.Instantiate<ItemPickup>();
				pickup.Item = item;

				_world.ItemPickupContainer.AddChild(pickup);
				pickup.GlobalPosition = data.Position;
			}
		}
	}

	public bool RequestPlace(ChunkObject data)
	{
		var chunk = _world.ActiveChunks[data.ChunkCoord];

		chunk.Objects.Add(data);
		EnqueueSpawn(data);

		_world.AddPlacedObject(data.ChunkCoord, new PlacedObjectRecord
		{
			DefinitionTypeId = data.Definition.StableId,
			TileCoord = data.TileCoord,
			Position = data.Position
		});

		if (_world.Multiplayer.IsServer())
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
						var state = _world.Sync.GetOrCreateStationState(data.TileCoord, data.Definition.StableId);
						station.BindState(state);

						break;
					}
					case IItemContainer storage:
					{
						var state = _world.Sync.GetOrCreateStorageState(data.TileCoord);
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