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

	public void BroadcastMobAttack(ulong uid, int comboIndex)
	{
		Rpc(nameof(PlayRemoteMobAttack), uid, comboIndex);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	public void PlayRemoteMobAttack(ulong uid, int comboIndex)
	{
		if (!_world.MobStreamer.TryGetMob(uid, out var mob))
			return;

		if (IsInstanceValid(mob))
			mob.PlayRemoteAttackVisual(comboIndex);
	}

	public void BroadcastMobStaggered(Mob mob, Vector3 fromDirection)
	{
		Rpc(nameof(ReceiveMobStaggered), mob.Uid, fromDirection);
	}

	[Rpc(CallLocal = false)]
	private void ReceiveMobStaggered(ulong uid, Vector3 fromDirection)
	{
		if (!_world.MobStreamer.TryGetMob(uid, out var mob))
			return;

		mob.ApplyRemoteStagger(fromDirection);
	}

	public void BroadcastMobBlockState(Mob mob, bool blockState)
	{
		Rpc(nameof(ReceiveMobBlockState), mob.Uid, blockState);
	}

	[Rpc(CallLocal = false)]
	private void ReceiveMobBlockState(ulong uid, bool blockState)
	{
		if (!_world.MobStreamer.TryGetMob(uid, out var mob)) return;

		if (mob is Bandit b) b.ApplyRemoteBlockState(blockState);
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
		const int step = 6;

		for (var i = 0; i < data.Count; i += step)
		{
			var uid = (ulong)data[i];
			var pos = (Vector3)data[i + 1];
			var rot = (Vector3)data[i + 2];
			var vel = (Vector3)data[i + 3];
			var state = (int)data[i + 4];
			var health = (float)data[i + 5];

			if (_world.MobStreamer.TryGetMob(uid, out var mob))
				mob.ApplyRemoteSnapshot(pos, rot, vel, state, health);
		}
	}
}