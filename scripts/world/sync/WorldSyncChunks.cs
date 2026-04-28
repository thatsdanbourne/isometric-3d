using Godot;

public partial class WorldSync
{
	private Vector2I? _lastSubmittedChunkCenter;

	public void SendChunkUnloadToPeer(int peerId, Vector2I chunkCoord)
	{
		var localPeerId = Multiplayer.GetUniqueId();

		if (peerId == localPeerId)
		{
			_world.ChunkManager.RemoveChunk(chunkCoord);
			return;
		}

		RpcId(peerId, nameof(ReceiveChunkUnload), chunkCoord);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void ReceiveChunkUnload(Vector2I chunkCoord)
	{
		if (!_world.ActiveChunks.ContainsKey(chunkCoord))
			return;

		_world.ChunkManager.RemoveChunk(chunkCoord);
	}

	public void SendChunkToPeer(int peerId, Chunk chunk)
	{
		var serialized = SerializationUtils.SerializeChunk(chunk);
		RpcId(peerId, nameof(ReceiveChunk), serialized);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	public void ReceiveChunk(Godot.Collections.Dictionary chunkData)
	{
		var chunk = SerializationUtils.DeserializeChunk(chunkData);
		_world.ChunkGenerator.EnqueueClientChunk(chunk);
	}

	public void UpdateLocalChunkInterest(Vector3 localPlayerPosition, int chunkRadius)
	{
		var center = TileUtils.WorldToChunk(localPlayerPosition);

		if (_lastSubmittedChunkCenter.HasValue && _lastSubmittedChunkCenter.Value == center)
			return;

		_lastSubmittedChunkCenter = center;

		var coords = BuildDesiredChunkArray(center, chunkRadius);

		RpcId(1, nameof(SubmitDesiredChunks), coords);
	}

	private Godot.Collections.Array<Vector2I> BuildDesiredChunkArray(Vector2I center, int chunkRadius)
	{
		var coords = new Godot.Collections.Array<Vector2I>();

		for (var x = -chunkRadius; x <= chunkRadius; x++)
		for (var y = -chunkRadius; y <= chunkRadius; y++)
			coords.Add(new Vector2I(center.X + x, center.Y + y));

		return coords;
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	public void SubmitDesiredChunks(Godot.Collections.Array<Vector2I> coords)
	{
		if (!Multiplayer.IsServer())
			return;

		var peerId = Multiplayer.GetRemoteSenderId();
		_world.ChunkManager.UpdatePeerInterest(peerId, coords);
	}
}