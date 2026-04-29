using System.Linq;
using Godot;

public partial class WorldSync
{
	public void HandleBreakObject(Vector2I chunkCoord, Vector2I tileCoord)
	{
		if (!Multiplayer.IsServer())
			return;

		if (!_world.WorldObjectManager.TryGetObject(chunkCoord, tileCoord, out var target))
			return;

		_world.WorldObjectManager.RequestBreak(target.Data);
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

		var def = item?.PlaceableObjectDefinition;
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

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	public void RequestInteractObject(Vector2I chunkCoord, Vector2I tileCoord)
	{
		if (!_world.Multiplayer.IsServer())
			return;

		var player = GetRequestingPlayer();

		if (player == null)
			return;

		if (!_world.WorldObjectManager.TryGetObject(chunkCoord, tileCoord, out var obj))
			return;

		if (obj is not IInteractable interactable)
			return;

		if (!interactable.CanInteract(player))
			return;

		interactable.Interact(player);
	}
}