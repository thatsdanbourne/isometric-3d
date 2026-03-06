using Godot;
using System;
using System.Collections.Generic;

public partial class MobStreamer : Node
{
	[Export] public int ActiveRadiusTiles = 48;
	[Export] public int SaveRadiusTiles = 56;
	[Export] public float UpdateInterval = 0.75f;

	private World _world;
	private Node3D _worldMobs;

	private readonly Dictionary<ulong, Mob> _activeMobs = new();
	private readonly HashSet<Vector2I> _activeMobChunks = new();
	private readonly HashSet<Vector2I> _pendingChunks = new();

	private double _accum;

	public override void _Ready()
	{
		_world = GetNode<World>("/root/Game/World");
		_worldMobs = _world.GetNode<Node3D>("WorldMobs");
	}

	public void OnChunkFinalised(Vector2I chunkCoord)
	{
		_pendingChunks.Add(chunkCoord);
	}

	public void OnChunkUnloaded(Vector2I chunkCoord)
	{
		_pendingChunks.Remove(chunkCoord);
		_activeMobChunks.Remove(chunkCoord);
	}

	public override void _Process(double delta)
	{
		_accum += delta;
		if (_accum < UpdateInterval) return;
		_accum = 0;

		ActivatePendingChunks();
		CullFarMobs();
	}

	private HashSet<Vector2I> ComputeTargetChunks(Player player)
	{
		var set = new HashSet<Vector2I>();
		var rChunks = Mathf.CeilToInt((float)ActiveRadiusTiles / _world.ChunkSize);

		var center = TileManager.WorldToChunk(player.GlobalPosition);

		for (var dx = -rChunks; dx <= rChunks; dx++)
		for (var dy = -rChunks; dy <= rChunks; dy++)
			set.Add(new Vector2I(center.X + dx, center.Y + dy));

		return set;
	}

	private void ActivatePendingChunks()
	{
		if (_pendingChunks.Count == 0) return;

		var interest = ComputeTargetChunks(_world.Player);

		var toCheck = new List<Vector2I>(_pendingChunks);
		foreach (var chunkCoord in toCheck)
		{
			if (!interest.Contains(chunkCoord)) continue;

			if (_activeMobChunks.Contains(chunkCoord)) continue;

			if (!_world.ActiveChunks.TryGetValue(chunkCoord, out var chunk)) continue;

			SpawnProceduralForChunk(chunk);
			_activeMobChunks.Add(chunkCoord);
			_pendingChunks.Remove(chunkCoord);
		}
	}

	private void SpawnProceduralForChunk(Chunk chunk)
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

				var instance = scene.Instantiate<Mob>();
				instance.SetUid(uid);
				instance.Position = TileManager.TileToWorld(picked[i]);
				_world.WorldMobs.AddChild(instance);

				_activeMobs[uid] = instance;
			}
		}
	}

	private void CullFarMobs()
	{
		foreach (var (uid, mob) in _activeMobs)
		{
			if (!IsInstanceValid(mob))
			{
				_activeMobs.Remove(uid);
				continue;
			}

			var mobTile = TileManager.WorldToTile(mob.GlobalPosition);
			var playerTile = TileManager.WorldToTile(_world.Player.GlobalPosition);

			var inRange = false;
			var distance = ChebyshevTileDistance(mobTile, playerTile);
			if (distance <= SaveRadiusTiles)
				inRange = true;

			if (inRange) continue;

			mob.QueueFree();
			_activeMobs.Remove(uid);
		}
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

	private static int ChebyshevTileDistance(Vector2I a, Vector2I b)
	{
		return (int)a.DistanceTo(b);
	}
}