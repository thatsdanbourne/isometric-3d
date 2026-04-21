using System.Collections.Generic;
using Godot;

public partial class WorldSync
{
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	public void RequestPickup(ulong pickupId)
	{
		if (!_world.Multiplayer.IsServer())
			return;

		var player = _world.GetPlayerById(Multiplayer.GetRemoteSenderId());
		if (player == null)
			return;

		_world.ItemDropManager.HandlePickupRequest(player, pickupId);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	public void RequestDropItem(string itemId, int count)
	{
		if (!_world.Multiplayer.IsServer())
			return;

		var player = _world.GetPlayerById(Multiplayer.GetRemoteSenderId());
		if (player == null)
			return;

		_world.ItemDropManager.HandleDropItemRequest(player, itemId, count);
	}

	public void BroadcastPickup(ItemPickupSpawnData data)
	{
		if (!_world.Multiplayer.IsServer())
			return;

		var payload = SerializationUtils.SerializePickup(
			data.PickupId,
			data.ItemId,
			data.Count,
			data.Position,
			data.InitialVelocity,
			data.InitialVerticalVelocity
		);

		Rpc(nameof(SpawnRemotePickup), payload);
	}

	public void BroadcastPickups(IReadOnlyList<ItemPickupSpawnData> data)
	{
		if (!_world.Multiplayer.IsServer() || data == null || data.Count == 0)
			return;

		var payload = _world.ItemDropManager.BuildDropPayload(data);
		Rpc(nameof(SpawnRemotePickups), payload);
	}

	public void BroadcastPickupRemoved(ulong pickupId)
	{
		if (!_world.Multiplayer.IsServer())
			return;

		Rpc(nameof(RemoveRemotePickup), pickupId);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	public void SpawnRemotePickup(Godot.Collections.Dictionary payload)
	{
		_world.ItemDropManager.SpawnPickupFromPayload(payload);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	public void SpawnRemotePickups(Godot.Collections.Array<Godot.Collections.Dictionary> payload)
	{
		_world.ItemDropManager.SpawnPickupsFromPayload(payload);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	public void RemoveRemotePickup(ulong pickupId)
	{
		_world.ItemDropManager.RemovePickupById(pickupId);
	}
}