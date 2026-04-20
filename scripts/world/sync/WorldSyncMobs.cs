using Godot;

public partial class WorldSync
{
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	public void SpawnRemoteMob(string uidString, string mobId, Vector2I chunkCoord, Vector3 position)
	{
		var uid = DeterministicHash.StringToUid(uidString);
		_world.MobStreamer.SpawnRemoteMob(uid, mobId, chunkCoord, position);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	public void RemoveRemoteMob(string uidString)
	{
		var uid = DeterministicHash.StringToUid(uidString);
		_world.MobStreamer.RemoveRemoteMob(uid);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	public void ReceiveMobSnapshot(string uidString, Vector3 position, Vector3 velocity, int state, float health)
	{
		var uid = DeterministicHash.StringToUid(uidString);

		if (!_world.MobStreamer.TryGetMob(uid, out var mob))
			return;

		mob.ApplyRemoteSnapshot(position, velocity, state, health);
	}

	public void BroadcastMobAttack(ulong uid)
	{
		Rpc(nameof(PlayRemoteMobAttack), DeterministicHash.UidToString(uid));
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	public void PlayRemoteMobAttack(string uidString)
	{
		var uid = DeterministicHash.StringToUid(uidString);

		if (!_world.MobStreamer.TryGetMob(uid, out var mob))
			return;

		if (IsInstanceValid(mob))
			mob.PlayRemoteAttackVisual();
	}

	public void BroadcastMobDeath(ulong uid)
	{
		Rpc(nameof(ApplyMobDeath), DeterministicHash.UidToString(uid));
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	public void ApplyMobDeath(string uidString)
	{
		var uid = DeterministicHash.StringToUid(uidString);

		if (!_world.MobStreamer.TryGetMob(uid, out var mob))
			return;

		mob.QueueFree();
	}
}