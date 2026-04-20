using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Array = Godot.Collections.Array;

public partial class MobStreamer : Node
{
	[Export] public int ActiveRadiusTiles = 48;
	[Export] public int SaveRadiusTiles = 56;
	[Export] public float UpdateInterval = 0.75f;
	[Export] public float SyncInterval = 0.05f;

	private World _world;

	private readonly Dictionary<ulong, Mob> _activeMobs = new();
	private readonly Dictionary<Vector2I, HashSet<ulong>> _mobsInChunk = new();
	private readonly Dictionary<int, HashSet<ulong>> _peerKnownMobs = new();
	private readonly HashSet<Vector2I> _activeMobChunks = new();

	private double _accum;
	private double _syncAccum;

	public override void _Ready()
	{
		_world ??= GetParent<World>();
		_world.Multiplayer.PeerDisconnected += (id) => _peerKnownMobs.Remove((int)id);
	}

	public void Tick(double delta)
	{
		_accum += delta;
		if (_accum >= UpdateInterval)
		{
			_accum = 0;
			ProcessChunkSimulation();
			CullFarMobs();
		}

		_syncAccum += delta;
		if (_syncAccum >= SyncInterval)
		{
			_syncAccum = 0;
			SyncWorldToPlayers();
		}
	}


	#region Simulation Logic

	private void ProcessChunkSimulation()
	{
		var currentInterest = new HashSet<Vector2I>();
		foreach (var player in _world.Players.Where(IsValidPlayer))
			currentInterest.UnionWith(ComputeTargetChunks(player.GlobalPosition));

		foreach (var coord in currentInterest)
			if (_activeMobChunks.Add(coord))
				ActivateChunkMobs(coord);

		var toRemove = _activeMobChunks.Where(c => !currentInterest.Contains(c)).ToList();
		foreach (var coord in toRemove)
		{
			DeactivateChunk(coord);
			_activeMobChunks.Remove(coord);
		}
	}

	private void DeactivateChunk(Vector2I coord)
	{
		if (_mobsInChunk.TryGetValue(coord, out var uids))
			foreach (var uid in uids.ToList())
				if (_activeMobs.TryGetValue(uid, out var mob))
				{
					SaveMobToDelta(mob);
					CleanupMob(mob);
				}
	}

	private void ActivateChunkMobs(Vector2I coord)
	{
		_world.TryGetChunkDelta(coord, out var delta);

		if (delta?.Mobs != null)
			foreach (var mobData in delta.Mobs.Values)
			{
				if (_activeMobs.ContainsKey(mobData.Uid))
					continue;

				SpawnMobFromRecord(mobData, coord);
			}

		if (_world.ActiveChunks.TryGetValue(coord, out var chunk))
			ActivateProceduralMobs(chunk, delta);
	}

	private void ActivateProceduralMobs(Chunk chunk, ChunkDeltaData delta)
	{
		var mobCandidates = new Dictionary<MobSpawnRule, List<Vector2I>>(16);

		for (var x = 0; x < chunk.Tiles.GetLength(0); x++)
		for (var y = 0; y < chunk.Tiles.GetLength(1); y++)
		{
			var tile = chunk.Tiles[x, y];

			if (tile.Definition.Id == TileId.Water) continue;

			var biome = RuleRegistry.GetBiomeById(tile.Biome);

			var rules = biome.MobRules;
			if (rules.Count == 0) continue;

			var globalX = chunk.Coord.X * _world.ChunkSize + x;
			var globalY = chunk.Coord.Y * _world.ChunkSize + y;
			var globalTile = new Vector2I(globalX, globalY);

			foreach (var rule in rules)
			{
				if (!mobCandidates.TryGetValue(rule, out var list))
					mobCandidates[rule] = list = new List<Vector2I>(8);

				list.Add(globalTile);
			}
		}

		var picked = new List<Vector2I>(8);

		foreach (var (rule, candidates) in mobCandidates)
		{
			if (candidates.Count == 0) continue;

			var seed = DeterministicHash.Combine32(
				_world.TerrainSeed,
				chunk.Coord.X,
				chunk.Coord.Y,
				rule.StableId
			);

			var rng = new Random(seed);

			if (rng.NextDouble() > rule.ChunkChance) continue;

			var count = rng.Next(rule.MinPerChunk, rule.MaxPerChunk + 1);
			count = Math.Min(count, candidates.Count);

			if (count <= 0) continue;

			PickUniqueTilesDeterministic(candidates, count, rng, picked);

			for (var i = 0; i < picked.Count; i++)
			{
				var tile = picked[i];
				var uid = DeterministicHash.Combine64(
					_world.TerrainSeed,
					chunk.Coord.X,
					chunk.Coord.Y,
					rule.StableId,
					tile.X,
					tile.Y
				);

				if (_activeMobs.ContainsKey(uid))
					continue;

				if (delta != null && delta.Mobs.ContainsKey(uid))
					continue;

				var position = TileUtils.TileToWorld(tile);
				SpawnMob(uid, rule.MobId, chunk.Coord, position);
			}
		}
	}

	#endregion


	#region Networking & Sync

	private void SyncWorldToPlayers()
	{
		foreach (var player in _world.Players.Where(IsValidPlayer))
		{
			var peerId = player.PlayerId;
			var visibleChunks = ComputeTargetChunks(player.GlobalPosition);
			SyncPeerEntities(peerId, visibleChunks);
		}
	}

	private void SyncPeerEntities(int peerId, HashSet<Vector2I> visibleChunks)
	{
		if (!_peerKnownMobs.TryGetValue(peerId, out var known))
		{
			known = new HashSet<ulong>();
			_peerKnownMobs[peerId] = known;
		}

		var snapshotBatch = new Array();
		var currentVisibleUids = new HashSet<ulong>();

		foreach (var coord in visibleChunks)
		{
			if (!_mobsInChunk.TryGetValue(coord, out var uids)) continue;

			foreach (var uid in uids)
			{
				if (!_activeMobs.TryGetValue(uid, out var mob)) continue;
				currentVisibleUids.Add(uid);


				if (known.Add(uid))
					_world.Sync.RpcId(peerId,
						nameof(WorldSync.SpawnRemoteMob),
						uid,
						mob.MobId,
						coord,
						mob.GlobalPosition
					);

				snapshotBatch.Add(uid);
				snapshotBatch.Add(mob.GlobalPosition);
				snapshotBatch.Add(mob.Velocity);
				snapshotBatch.Add((int)mob.State);
				snapshotBatch.Add(mob.CurrentHealth);
			}
		}

		if (snapshotBatch.Count > 0)
			_world.Sync.RpcId(peerId, nameof(WorldSync.ReceiveMobBatch), snapshotBatch);

		var toRemove = known.Where(uid => !currentVisibleUids.Contains(uid)).ToList();
		foreach (var uid in toRemove)
		{
			known.Remove(uid);
			_world.Sync.RpcId(peerId, nameof(WorldSync.RemoveRemoteMob), uid);
		}
	}

	public void SpawnRemoteMob(ulong uid, string mobId, Vector2I chunkCoord, Vector3 position)
	{
		if (_activeMobs.ContainsKey(uid))
			return;

		var scene = MobRegistry.Instance.GetScene(mobId);
		if (scene == null)
			return;

		var instance = scene.Instantiate<Mob>();
		instance.Initialise(uid, mobId, chunkCoord);
		instance.World = _world;
		instance.Position = position;
		instance.RuntimeChunk = chunkCoord;

		_world.WorldMobs.AddChild(instance);
		_activeMobs[uid] = instance;
		RegisterMobInChunkLookup(chunkCoord, uid);
	}

	public void RemoveRemoteMob(ulong uid)
	{
		if (_activeMobs.TryGetValue(uid, out var mob))
			CleanupMob(mob);
	}

	#endregion


	#region Lifecycle

	public void HandleMobDeath(Mob mob)
	{
		if (mob == null) return;

		if (_world.TryGetChunkDelta(mob.RuntimeChunk, out var delta))
			delta.Mobs.Remove(mob.Uid);

		foreach (var (peerId, known) in _peerKnownMobs)
			if (known.Remove(mob.Uid))
				_world.Sync.RpcId(peerId, nameof(WorldSync.RemoveRemoteMob), mob.Uid);

		CleanupMob(mob);
	}

	private void CullFarMobs()
	{
		var playerPositions = _world.Players.Where(IsValidPlayer).Select(p => p.GlobalPosition).ToList();
		var toDespawn = new List<ulong>();

		foreach (var (uid, mob) in _activeMobs)
		{
			var inRange = playerPositions.Any(p =>
				p.DistanceTo(mob.GlobalPosition) <= SaveRadiusTiles);

			if (!inRange) toDespawn.Add(uid);
		}

		foreach (var uid in toDespawn)
			if (_activeMobs.TryGetValue(uid, out var mob))
			{
				SaveMobToDelta(mob);
				CleanupMob(mob);
			}
	}

	private void CleanupMob(Mob mob)
	{
		RemoveMobFromChunkLookup(mob.RuntimeChunk, mob.Uid);
		_activeMobs.Remove(mob.Uid);
		if (IsInstanceValid(mob)) mob.QueueFree();
	}

	private void SaveMobToDelta(Mob mob)
	{
		var delta = _world.GetOrCreateChunkDelta(mob.RuntimeChunk);
		GD.Print($"Saving mob {mob.Uid} to delta at {mob.Position}");
		delta.Mobs[mob.Uid] = new MobRecord
		{
			Uid = mob.Uid,
			MobId = mob.MobId,
			Position = mob.GlobalPosition
		};
	}

	#endregion


	#region Spawning

	private void SpawnMobFromRecord(MobRecord mobData, Vector2I chunkCoord)
	{
		if (_activeMobs.ContainsKey(mobData.Uid))
			return;
		GD.Print($"Spawning mob {mobData.Uid} at {mobData.Position} from record");

		var scene = MobRegistry.Instance.GetScene(mobData.MobId);
		if (scene == null)
			return;

		var instance = scene.Instantiate<Mob>();
		instance.LoadFromSaveData(mobData, chunkCoord);
		instance.World = _world;
		instance.Position = mobData.Position;
		instance.RuntimeChunk = chunkCoord;

		_world.WorldMobs.AddChild(instance);
		_activeMobs[mobData.Uid] = instance;
		RegisterMobInChunkLookup(chunkCoord, mobData.Uid);
	}


	private void SpawnMob(ulong uid, string mobId, Vector2I chunkCoord, Vector3 position)
	{
		if (_activeMobs.ContainsKey(uid))
			return;

		var scene = MobRegistry.Instance.GetScene(mobId);
		if (scene == null)
			return;

		var instance = scene.Instantiate<Mob>();
		instance.Initialise(uid, mobId, chunkCoord);
		instance.World = _world;
		instance.Position = position;
		instance.RuntimeChunk = chunkCoord;

		_world.WorldMobs.AddChild(instance);
		_activeMobs[uid] = instance;
		RegisterMobInChunkLookup(chunkCoord, uid);
	}

	#endregion


	#region Helpers

	public void UpdateMobChunkMembership(Mob mob)
	{
		var newChunk = TileUtils.WorldToChunk(mob.GlobalPosition);
		if (mob.RuntimeChunk == newChunk) return;

		RemoveMobFromChunkLookup(mob.RuntimeChunk, mob.Uid);
		mob.RuntimeChunk = newChunk;
		RegisterMobInChunkLookup(newChunk, mob.Uid);
	}

	private void RegisterMobInChunkLookup(Vector2I coord, ulong uid)
	{
		if (!_mobsInChunk.ContainsKey(coord)) _mobsInChunk[coord] = new HashSet<ulong>();
		_mobsInChunk[coord].Add(uid);
	}

	private void RemoveMobFromChunkLookup(Vector2I coord, ulong uid)
	{
		if (_mobsInChunk.TryGetValue(coord, out var set))
		{
			set.Remove(uid);
			if (set.Count == 0) _mobsInChunk.Remove(coord);
		}
	}

	private HashSet<Vector2I> ComputeTargetChunks(Vector3 worldPosition)
	{
		var set = new HashSet<Vector2I>();
		var rChunks = Mathf.CeilToInt((float)ActiveRadiusTiles / _world.ChunkSize);

		var center = TileUtils.WorldToChunk(worldPosition);

		for (var dx = -rChunks; dx <= rChunks; dx++)
		for (var dy = -rChunks; dy <= rChunks; dy++)
			set.Add(new Vector2I(center.X + dx, center.Y + dy));

		return set;
	}

	private void PickUniqueTilesDeterministic(List<Vector2I> candidates, int count, Random rng, List<Vector2I> picked)
	{
		picked.Clear();

		var temp = new List<Vector2I>(candidates);

		for (var i = 0; i < count; i++)
		{
			var j = rng.Next(i, temp.Count);
			(temp[i], temp[j]) = (temp[j], temp[i]);
			picked.Add(temp[i]);
		}
	}

	private static bool IsValidPlayer(Player p)
	{
		return p != null && IsInstanceValid(p) && p.IsInsideTree();
	}

	public bool TryGetMob(ulong uid, out Mob mob)
	{
		return _activeMobs.TryGetValue(uid, out mob);
	}

	public IEnumerable<Mob> GetActiveMobs()
	{
		return _activeMobs.Values;
	}

	#endregion
}