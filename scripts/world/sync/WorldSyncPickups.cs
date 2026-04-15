using Godot;

public partial class WorldSync
{
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	public void RequestDropItem(string itemId, int count)
	{
		if (!Multiplayer.IsServer())
			return;

		if (count <= 0)
			return;

		var player = GetRequestingPlayer();
		if (player == null)
			return;

		_world.WorldObjectManager.HandleDropItemRequest(player, itemId, count);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	public void SpawnRemotePickups(Godot.Collections.Array<Godot.Collections.Dictionary> pickups)
	{
		foreach (var p in pickups) _world.WorldObjectManager.SpawnPickupFromData(p);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	public void RequestPickup(ulong pickupId)
	{
		if (!Multiplayer.IsServer())
			return;

		var player = GetRequestingPlayer();
		if (player == null)
			return;

		_world.WorldObjectManager.HandlePickupRequest(player, pickupId);
	}

	public void BroadcastPickupRemoved(ItemPickup pickup)
	{
		var chunk = TileUtils.WorldToChunk(pickup.GlobalPosition);
		var peers = _world.ChunkManager.GetPeersInterestedInChunk(chunk);

		foreach (var peerId in peers)
			RpcId(peerId, nameof(RemoveRemotePickup), pickup.PickupId);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void RemoveRemotePickup(ulong pickupId)
	{
		_world.WorldObjectManager.RemovePickupById(pickupId);
	}
}