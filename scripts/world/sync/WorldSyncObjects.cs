using Godot;

public partial class WorldSync
{
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	public void RequestBreakObject(Vector2I chunkCoord, Vector2I tileCoord)
	{
		if (!Multiplayer.IsServer())
			return;

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

		_world.WorldObjectManager.RequestBreak(target);
	}

	public void BroadcastObjectRemoved(Vector2I chunkCoord, Vector2I tileCoord)
	{
		if (!Multiplayer.IsServer())
			return;

		foreach (var peerId in _world.ChunkManager.GetPeersInterestedInChunk(chunkCoord))
			RpcId(peerId, nameof(ReceiveObjectRemoved), chunkCoord, tileCoord);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	public void RequestPlaceObject(string itemId, int defId, Vector2I chunkCoord, Vector2I tileCoord, Vector3 worldPos)
	{
		if (!Multiplayer.IsServer())
			return;

		var player = GetRequestingPlayer();
		if (player == null)
			return;

		var item = ItemRegistry.GetItem(itemId) as PlaceableItem;
		if (item == null)
			return;

		var def = item.PlaceableObjectDefinition;
		if (def == null)
			return;

		_world.TryPlaceItem(player, item, defId, tileCoord, chunkCoord, worldPos);
	}

	public void BroadcastObjectPlaced(ChunkObject data)
	{
		if (!Multiplayer.IsServer())
			return;

		foreach (var peerId in _world.ChunkManager.GetPeersInterestedInChunk(data.ChunkCoord))
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
		_world.WorldObjectManager.ApplyRemoteBreak(chunkCoord, tileCoord);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void ReceiveObjectPlaced(int defId, Vector2I chunkCoord, Vector2I tileCoord, Vector3 worldPos)
	{
		_world.WorldObjectManager.ApplyRemotePlace(defId, chunkCoord, tileCoord, worldPos);
	}
}