using Godot;
using System.Collections.Generic;

public partial class ItemDropManager : Node
{
	[Export] public PackedScene PickupScene;

	private World _world;
	private readonly Dictionary<ulong, ItemPickup> _activePickups = new();
	private readonly RandomNumberGenerator _rng = new();

	private ulong _nextPickupId = 1;

	public override void _Ready()
	{
		_world = GetParent<World>();
		_rng.Randomize();
		PickupScene ??= GD.Load<PackedScene>("res://scenes/ItemPickup.tscn");
	}

	public ItemPickupSpawnData CreatePickupData(string itemId, int count, Vector3 position)
	{
		return new ItemPickupSpawnData
		{
			PickupId = _nextPickupId++,
			ItemId = itemId,
			Count = count,
			Position = position,
			InitialVelocity = RandomDir() * ItemPickup.LaunchStrength,
			InitialVerticalVelocity = ItemPickup.BounceHeight * 8f
		};
	}

	public ItemPickupSpawnData CreatePickupData(
		string itemId,
		int count,
		Vector3 position,
		Vector3 initialVelocity,
		float initialVerticalVelocity
	)
	{
		return new ItemPickupSpawnData
		{
			PickupId = _nextPickupId++,
			ItemId = itemId,
			Count = count,
			Position = position,
			InitialVelocity = initialVelocity,
			InitialVerticalVelocity = initialVerticalVelocity
		};
	}

	public void HandleDropItemRequest(Player player, string itemId, int count)
	{
		if (!_world.Multiplayer.IsServer())
			return;

		if (player == null || count <= 0)
			return;

		var stack = player.DraggedStack;
		if (stack?.Item == null || stack.Count <= 0)
			return;

		var data = CreatePickupData(
			stack.Item.Id,
			stack.Count,
			player.GlobalPosition
		);

		SpawnPickup(data, true);

		player.DraggedStack = null;
		_world.Sync.SyncPlayerInventoryState(player);
	}

	public void HandlePickupRequest(Player player, ulong pickupId)
	{
		if (!_world.Multiplayer.IsServer())
			return;

		if (player == null)
			return;

		if (!_activePickups.Remove(pickupId, out var pickup))
			return;

		if (!IsInstanceValid(pickup))
			return;

		player.GiveItem(pickup.Item, pickup.Count);
		_world.Sync.SyncPlayerInventoryState(player);

		pickup.AnimateOut();
		_world.Sync.BroadcastPickupRemoved(pickupId);
	}

	public void SpawnPickup(ItemPickupSpawnData data, bool broadcast = true)
	{
		SpawnPickupLocal(data);

		if (broadcast)
			_world.Sync.BroadcastPickup(data);
	}

	public void SpawnPickups(IReadOnlyList<ItemPickupSpawnData> data, bool broadcast = true)
	{
		if (data == null || data.Count == 0)
			return;

		foreach (var entry in data)
			SpawnPickupLocal(entry);

		if (broadcast)
			_world.Sync.BroadcastPickups(data);
	}

	public void SpawnPickupLocal(ItemPickupSpawnData data)
	{
		if (_activePickups.ContainsKey(data.PickupId))
			return;

		var item = ItemRegistry.GetItem(data.ItemId);
		if (item == null)
			return;

		if (PickupScene == null)
			return;

		var pickup = PickupScene.Instantiate<ItemPickup>();
		pickup.PickupId = data.PickupId;
		pickup.Item = item;
		pickup.Count = data.Count;
		pickup.Position = data.Position;
		pickup.InitialVelocity = data.InitialVelocity;
		pickup.InitialVerticalVelocity = data.InitialVerticalVelocity;

		_world.ItemPickupContainer.AddChild(pickup);
		_activePickups[pickup.PickupId] = pickup;
	}

	public void SpawnPickupFromPayload(Godot.Collections.Dictionary payload)
	{
		var data = SerializationUtils.DeserializePickup(payload);
		SpawnPickupLocal(data);
	}

	public void SpawnPickupsFromPayload(Godot.Collections.Array<Godot.Collections.Dictionary> payload)
	{
		foreach (var entry in payload)
			SpawnPickupFromPayload(entry);
	}

	public void RemovePickupById(ulong pickupId)
	{
		if (!_activePickups.Remove(pickupId, out var pickup))
			return;

		if (IsInstanceValid(pickup))
			pickup.AnimateOut();
	}

	public Godot.Collections.Array<Godot.Collections.Dictionary> BuildDropPayload(
		IReadOnlyList<ItemPickupSpawnData> data)
	{
		var result = new Godot.Collections.Array<Godot.Collections.Dictionary>();

		if (data == null)
			return result;

		foreach (var d in data)
			result.Add(
				SerializationUtils.SerializePickup(
					d.PickupId,
					d.ItemId,
					d.Count,
					d.Position,
					d.InitialVelocity,
					d.InitialVerticalVelocity
				)
			);

		return result;
	}

	public void SendNearbyPickupsToPeer(int peerId, Vector3 center, float radius)
	{
		if (!_world.Multiplayer.IsServer())
			return;

		var nearby = new List<ItemPickupSpawnData>();

		foreach (var pickup in _activePickups.Values)
		{
			if (pickup == null || !IsInstanceValid(pickup) || !pickup.IsInsideTree())
				continue;

			if (pickup.GlobalPosition.DistanceTo(center) > radius)
				continue;

			nearby.Add(new ItemPickupSpawnData
			{
				PickupId = pickup.PickupId,
				ItemId = pickup.Item.Id,
				Count = pickup.Count,
				Position = pickup.GlobalPosition,
				InitialVelocity = Vector3.Zero,
				InitialVerticalVelocity = 0f
			});
		}

		if (nearby.Count == 0)
			return;

		var payload = BuildDropPayload(nearby);
		_world.Sync.RpcId(peerId, nameof(WorldSync.SpawnRemotePickups), payload);
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
}