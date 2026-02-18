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
		var delta = _world.GetOrCreateChunkDelta(data.ChunkCoord);

		if (data.Source == ChunkObjectSource.Procedural)
			delta.RemovedProceduralObjects.Add(data.TileCoord);
		else if (data.Source == ChunkObjectSource.Placed)
			delta.PlacedObjects.Remove(data);

		delta.StorageStates.Remove(data.TileCoord);

		EnqueueRemoval(data);
	}

	private void SpawnDrops(ChunkObject data)
	{
		if (data.RuntimeNode is not { } wo)
			return;

		var drops = wo.DropItems;
		if (drops == null || drops.Count == 0) return;

		foreach (var entry in drops)
		{
			if (GD.Randf() > entry.Chance)
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

		var chunkDelta = _world.GetOrCreateChunkDelta(data.ChunkCoord);
		chunkDelta.PlacedObjects.Add(data);

		return true;
	}

	public void EnqueueRemoval(ChunkObject data)
	{
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

			var node = WorldObjectRegistry.GetScene(data.Definition.Id).Instantiate<WorldObject>();

			node.Reset();
			node.Data = data;
			node.World = _world;
			data.RuntimeNode = node;

			node.Initialise(data.Definition);

			if (node.GetParent() != null)
				node.Reparent(_world.WorldObjects);
			else
				_world.WorldObjects.AddChild(node);

			node.Visible = true;
			node.SetPhysicsProcess(true);

			node.GlobalPosition = data.Position;
			node.Translate(new Vector3(0f, 0f, _rng.RandfRange(-0.01f, 0.01f)));

			if (data.Definition.BlocksTile)
				_world.BlockTile(data.TileCoord);

			// Restore state if applicable
			_world.TryGetChunkDelta(data.ChunkCoord, out var delta);

			if (node is IChunkStateful<StationStateData> station)
			{
				if (delta.StationStates.TryGetValue(data.TileCoord, out var stationState))
					station.RestoreState(stationState);
			}
			else if (node is IChunkStateful<StorageStateData> storage)
			{
				if (delta.StorageStates.TryGetValue(data.TileCoord, out var storageState))
					storage.RestoreState(storageState);
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

			if (data.RuntimeNode != null)
			{
				if (data.Definition.BlocksTile)
					_world.UnblockTile(data.TileCoord);

				Recycle(data.RuntimeNode);
				data.RuntimeNode = null;
			}

			count++;
		}
	}

	// private WorldObject GetPooled(string sceneId)
	// {
	// 	if (_pools.TryGetValue(sceneId, out var stack) && stack.Count > 0)
	// 		return stack.Pop();
	//
	// 	return WorldObjectRegistry.GetScene(sceneId).Instantiate<WorldObject>();
	// }

	private void Recycle(WorldObject node)
	{
		// node.Visible = false;
		// node.SetPhysicsProcess(false);
		// node.Reparent(_world.WorldObjectPool);

		// var id = node.Data.Definition.Id;

		// if (!pools.TryGetValue(id, out var stack))
		//     pools[id] = stack = new Stack<WorldObjectBase>();

		// if (stack.Count < maxPoolSizePerType)
		//     stack.Push(node);
		// else
		//     node.QueueFree();

		node.QueueFree();
	}
}