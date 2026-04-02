using Godot;
using System;
using System.Collections.Generic;

public partial class MobStreamer : Node
{
	[Export] public int ActiveRadiusTiles = 48;
	[Export] public int SaveRadiusTiles = 56;
	[Export] public float UpdateInterval = 0.75f;

	private World _world;

	private readonly Dictionary<ulong, Mob> _activeMobs = new();
	private readonly List<ulong> _uidsToCull = new();
	private readonly HashSet<Vector2I> _activeMobChunks = new();
	private readonly HashSet<Vector2I> _currentInterestChunks = new();

	private double _accum;

	public override void _Ready()
	{
		_world ??= GetParent().GetParent<World>();
	}

	public override void _Process(double delta)
	{
		if (_world == null) return;

		_accum += delta;
		if (_accum < UpdateInterval) return;
		_accum = 0;

		UpdateInterestChunks();
		ActivateInterestChunks();
		CullFarMobs();
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
			SpawnDeltaMobs(chunk.Coord, delta);

		SpawnProceduralForChunk(chunk);
	}

	private void SpawnProceduralForChunk(Chunk chunk)
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

			var rng = new Random(HashSeed(_world.TerrainSeed, chunk.Coord, rule.Id));

			if (rng.NextDouble() > rule.ChunkChance) continue;

			var count = rng.Next(rule.MinPerChunk, rule.MaxPerChunk + 1);
			count = Math.Min(count, candidates.Count);

			if (count <= 0) continue;

			PickUniqueTilesDeterministic(candidates, count, rng, picked);

			var scene = MobRegistry.Instance.GetScene(rule.MobId);
			for (var i = 0; i < picked.Count; i++)
			{
				var uid = HashUid(_world.TerrainSeed, chunk.Coord, rule.Id, i);
				if (_activeMobs.ContainsKey(uid)) continue;

				if (delta != null && delta.Mobs.ContainsKey(uid)) continue;

				var instance = scene.Instantiate<Mob>();
				instance.Initialise(uid, rule.MobId, chunk.Coord);
				instance.World = _world;
				instance.Position = TileUtils.TileToWorld(picked[i]);
				_world.WorldMobs.AddChild(instance);

				_activeMobs[uid] = instance;
			}
		}
	}

	private void SpawnDeltaMobs(Vector2I chunkCoord, ChunkDeltaData delta)
	{
		foreach (var mob in delta.Mobs.Values)
		{
			if (_activeMobs.ContainsKey(mob.Uid)) continue;

			var scene = MobRegistry.Instance.GetScene(mob.MobId);
			var instance = scene.Instantiate<Mob>();
			instance.LoadFromSaveData(mob, chunkCoord);
			instance.World = _world;
			instance.Position = mob.Position;
			_world.WorldMobs.AddChild(instance);
			_activeMobs[mob.Uid] = instance;
		}
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
				mob.QueueFree();
			}

			_activeMobs.Remove(uid);
		}
	}

	public void HandleMobDeath(Mob mob)
	{
		if (mob.SavedChunk.HasValue && _world.TryGetChunkDelta(mob.SavedChunk.Value, out var delta) &&
		    delta != null)
			delta.Mobs.Remove(mob.Uid);

		_activeMobs.Remove(mob.Uid);

		if (IsInstanceValid(mob)) mob.QueueFree();
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

	private void PickUniqueTilesDeterministic(List<Vector2I> candidates, int count, Random rng, List<Vector2I> picked)
	{
		picked.Clear();

		// Fisher-Yates partial shuffle (in-place)
		for (var i = 0; i < count; i++)
		{
			var j = rng.Next(i, candidates.Count);
			(candidates[i], candidates[j]) = (candidates[j], candidates[i]);
			picked.Add(candidates[i]);
		}
	}

	// utility functions

	private static int StableHash(string s)
	{
		unchecked
		{
			var hash = 23;
			foreach (var t in s)
				hash = hash * 31 + t;

			return hash;
		}
	}

	private static int HashSeed(int worldSeed, Vector2I chunkCoord, string ruleId)
	{
		unchecked
		{
			var hash = worldSeed;
			hash = hash * 31 + chunkCoord.X;
			hash = hash * 31 + chunkCoord.Y;
			hash = hash * 31 + StableHash(ruleId);
			return hash;
		}
	}

	private static ulong HashUid(int worldSeed, Vector2I chunk, string ruleId, int index)
	{
		unchecked
		{
			var h = 1469598103934665603UL; // FNV-1a 64 offset basis

			Mix((uint)worldSeed);
			Mix((uint)chunk.X);
			Mix((uint)chunk.Y);
			Mix((uint)StableHash(ruleId));
			Mix((uint)index);

			return h;

			void Mix(ulong v)
			{
				h ^= v;
				h *= 1099511628211UL;
			}
		}
	}
}