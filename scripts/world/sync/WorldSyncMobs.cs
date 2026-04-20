using Godot;

public partial class WorldSync
{
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	public void SpawnRemoteMob(ulong uid, string mobId, Vector2I chunkCoord, Vector3 position)
	{
		_world.MobStreamer.SpawnRemoteMob(uid, mobId, chunkCoord, position);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	public void RemoveRemoteMob(ulong uid)
	{
		_world.MobStreamer.RemoveRemoteMob(uid);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	public void ReceiveMobSnapshot(ulong uid, Vector3 position, Vector3 velocity, int state, float health)
	{
		if (!_world.MobStreamer.TryGetMob(uid, out var mob))
			return;

		mob.ApplyRemoteSnapshot(position, velocity, state, health);
	}

	public void BroadcastMobAttack(ulong uid)
	{
		Rpc(nameof(PlayRemoteMobAttack), uid);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	public void PlayRemoteMobAttack(ulong uid)
	{
		if (!_world.MobStreamer.TryGetMob(uid, out var mob))
			return;

		if (IsInstanceValid(mob))
			mob.PlayRemoteAttackVisual();
	}

	public void BroadcastMobDeath(ulong uid)
	{
		Rpc(nameof(ApplyMobDeath), uid);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	public void ApplyMobDeath(ulong uid)
	{
		if (!_world.MobStreamer.TryGetMob(uid, out var mob))
			return;

		mob.QueueFree();
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
	public void ReceiveMobBatch(Godot.Collections.Array data)
	{
		const int step = 5;

		for (var i = 0; i < data.Count; i += step)
		{
			var uid = (ulong)data[i];
			var pos = (Vector3)data[i + 1];
			var vel = (Vector3)data[i + 2];
			var state = (int)data[i + 3];
			var health = (float)data[i + 4];

			if (_world.MobStreamer.TryGetMob(uid, out var mob))
				mob.ApplyRemoteSnapshot(pos, vel, state, health);
		}
	}
}