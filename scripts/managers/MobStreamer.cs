using Godot;
using System;
using System.Collections.Generic;

public partial class MobStreamer : Node
{
	[Export] public int ActiveRadiusTiles = 48;
	[Export] public int SaveRadiusTiles = 56;
	[Export] public float UpdateInterval = 0.75f;
	[Export] public float SyncInterval = 0.05f;

	private World _world;

	private readonly Dictionary<ulong, Mob> _activeMobs = new();
	private readonly Dictionary<Vector2I, HashSet<ulong>> _activeMobIdsByChunk = new();
	private readonly List<ulong> _uidsToCull = new();
	private readonly HashSet<Vector2I> _activeMobChunks = new();
	private readonly HashSet<Vector2I> _currentInterestChunks = new();

	private double _accum;
	private double _syncAccum;

	public override void _Ready()
	{
		_world ??= GetParent<World>();
	}

	public void Tick(double delta)
	{
		if (_world == null)
			return;

		_accum += delta;
		if (_accum >= UpdateInterval)
		{
			_accum = 0;
			UpdateInterestChunks();
			ActivateInterestChunks();
			CullFarMobs();
		}

		_syncAccum += delta;
		if (_syncAccum >= SyncInterval)
		{
			_syncAccum = 0;
			BroadcastMobSnapshots();
		}
	}

	private void BroadcastMobSnapshots()
	{
		foreach (var (uid, mob) in _activeMobs)
		{
			if (!IsInstanceValid(mob))
				continue;

			_world.Sync.Rpc(
				nameof(WorldSync.ReceiveMobSnapshot),
				uid.ToString(),
				mob.GlobalPosition,
				mob.Velocity,
				(int)mob.State,
				mob.CurrentHealth
			);
		}
	}

	private void UpdateInterestChunks()
	{
		_currentInterestChunks.Clear();

		foreach (var player in _world.Players)
		{
			if (!IsValidPlayer(player)) continue;
			_currentInterestChunks.UnionWith(ComputeTargetChunks(player.GlobalPosition));
		}
	}

	private void ActivateInterestChunks()
	{
		foreach (var chunkCoord in _currentInterestChunks)
		{
			if (_activeMobChunks.Contains(chunkCoord))
				continue;

			if (!_world.ActiveChunks.TryGetValue(chunkCoord, out var chunk))
				continue;

			ActivateChunkMobs(chunk);
			_activeMobChunks.Add(chunkCoord);
		}

		_activeMobChunks.RemoveWhere(chunkCoord => !_currentInterestChunks.Contains(chunkCoord));
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

	private void ActivateChunkMobs(Chunk chunk)
	{
		if (_world.TryGetChunkDelta(chunk.Coord, out var delta) && delta != null)
			ActivateDeltaMobs(chunk.Coord, delta);

		ActivateProceduralMobs(chunk);
	}

	private void ActivateProceduralMobs(Chunk chunk)
	{
		var mobCandidates = new Dictionary<MobSpawnRule, List<Vector2I>>(16);

		_world.TryGetChunkDelta(chunk.Coord, out var delta);

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

	private void ActivateDeltaMobs(Vector2I chunkCoord, ChunkDeltaData delta)
	{
		if (delta.Mobs == null || delta.Mobs.Count == 0)
			return;

		foreach (var (_, mobData) in delta.Mobs)
		{
			if (_activeMobs.ContainsKey(mobData.Uid))
				continue;

			SpawnMobFromData(chunkCoord, mobData);
		}
	}

	private void SpawnMobFromData(Vector2I chunkCoord, MobRecord mob)
	{
		if (_activeMobs.ContainsKey(mob.Uid))
			return;

		var scene = MobRegistry.Instance.GetScene(mob.MobId);
		if (scene == null)
			return;

		var instance = scene.Instantiate<Mob>();
		instance.LoadFromSaveData(mob, chunkCoord);
		instance.World = _world;
		instance.Position = mob.Position;

		_world.WorldMobs.AddChild(instance);
		_activeMobs[mob.Uid] = instance;
		RegisterMobInChunk(chunkCoord, mob.Uid);

		BroadcastMobSpawn(instance, chunkCoord);
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

		_world.WorldMobs.AddChild(instance);
		_activeMobs[uid] = instance;
		RegisterMobInChunk(chunkCoord, uid);

		BroadcastMobSpawn(instance, chunkCoord);
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

		_world.WorldMobs.AddChild(instance);
		_activeMobs[uid] = instance;
		RegisterMobInChunk(chunkCoord, uid);
	}

	private void BroadcastMobSpawn(Mob mob, Vector2I chunkCoord)
	{
		_world.Sync.Rpc(
			nameof(WorldSync.SpawnRemoteMob),
			mob.Uid.ToString(),
			mob.MobId,
			chunkCoord,
			mob.GlobalPosition
		);
	}

	private void CullFarMobs()
	{
		var playerTiles = GetValidPlayerTiles();
		_uidsToCull.Clear();

		foreach (var (uid, mob) in _activeMobs)
		{
			if (!IsInstanceValid(mob))
			{
				_uidsToCull.Add(uid);
				continue;
			}

			if (playerTiles.Count == 0)
			{
				_uidsToCull.Add(uid);
				continue;
			}

			var mobTile = TileUtils.WorldToTile(mob.GlobalPosition);

			if (!IsInRangeOfAnyPlayer(mobTile, playerTiles))
				_uidsToCull.Add(uid);
		}

		foreach (var uid in _uidsToCull)
		{
			if (!_activeMobs.TryGetValue(uid, out var mob)) continue;

			if (IsInstanceValid(mob))
			{
				SaveMobToDelta(mob);
				RemoveMobFromChunk(mob.SavedChunk ?? TileUtils.WorldToChunk(mob.GlobalPosition), uid);
				mob.QueueFree();
			}

			_activeMobs.Remove(uid);
		}
	}

	public void HandleMobDeath(Mob mob)
	{
		if (mob == null)
			return;

		if (mob.SavedChunk.HasValue && _world.TryGetChunkDelta(mob.SavedChunk.Value, out var delta) && delta != null)
			delta.Mobs.Remove(mob.Uid);

		_world.Sync.BroadcastMobDeath(mob.Uid);

		RemoveMobFromChunk(mob.SavedChunk ?? TileUtils.WorldToChunk(mob.GlobalPosition), mob.Uid);
		_activeMobs.Remove(mob.Uid);

		if (IsInstanceValid(mob))
			mob.QueueFree();
	}

	private void SaveMobToDelta(Mob mob)
	{
		var chunkCoord = TileUtils.WorldToChunk(mob.GlobalPosition);

		if (mob.SavedChunk.HasValue && _world.TryGetChunkDelta(mob.SavedChunk.Value, out var oldDelta) &&
		    oldDelta != null)
			oldDelta.Mobs.Remove(mob.Uid);

		var newDelta = _world.GetOrCreateChunkDelta(chunkCoord);
		newDelta.Mobs[mob.Uid] = new MobRecord
		{
			Uid = mob.Uid,
			MobId = mob.MobId,
			Position = mob.GlobalPosition
		};

		mob.SavedChunk = chunkCoord;
	}

	private List<Vector2I> GetValidPlayerTiles()
	{
		var result = new List<Vector2I>();

		foreach (var player in _world.Players)
		{
			if (!IsValidPlayer(player))
				continue;

			result.Add(TileUtils.WorldToTile(player.GlobalPosition));
		}

		return result;
	}

	private bool IsInRangeOfAnyPlayer(Vector2I mobTile, List<Vector2I> playerTiles)
	{
		foreach (var playerTile in playerTiles)
		{
			var dx = Math.Abs(playerTile.X - mobTile.X);
			var dy = Math.Abs(playerTile.Y - mobTile.Y);

			if (Math.Max(dx, dy) <= SaveRadiusTiles)
				return true;
		}

		return false;
	}

	private static bool IsValidPlayer(Player player)
	{
		return player != null && IsInstanceValid(player) && player.IsInsideTree();
	}

	private void RegisterMobInChunk(Vector2I chunkCoord, ulong uid)
	{
		if (!_activeMobIdsByChunk.TryGetValue(chunkCoord, out var set))
		{
			set = new HashSet<ulong>();
			_activeMobIdsByChunk[chunkCoord] = set;
		}

		set.Add(uid);
	}

	private void RemoveMobFromChunk(Vector2I chunkCoord, ulong uid)
	{
		if (!_activeMobIdsByChunk.TryGetValue(chunkCoord, out var set))
			return;

		set.Remove(uid);

		if (set.Count == 0)
			_activeMobIdsByChunk.Remove(chunkCoord);
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

	public bool TryGetMob(ulong uid, out Mob mob)
	{
		return _activeMobs.TryGetValue(uid, out mob);
	}

	public IEnumerable<Mob> GetActiveMobs()
	{
		return _activeMobs.Values;
	}
}