using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

public partial class ChunkGenerator(World world, ChunkManager chunkManager, int terrainSeed) : Node
{
	[Signal]
	public delegate void InitialChunksReadyEventHandler();

	private Thread _workerThread;
	private bool _running;

	private readonly ConcurrentQueue<Vector2I> _buildQueue = new();
	private readonly ConcurrentQueue<Chunk> _finaliseQueue = new();
	private RandomNumberGenerator _rng = new();

	private readonly Dictionary<string, int[]> _tileVariants = new()
	{
		{ "grass", [0, 1] },
		{ "sand", [3] },
		{ "snow", [4] }
	};

	private readonly Dictionary<string, float[]> _tileVariantWeights = new()
	{
		{ "grass", [0.795f, 0.005f] },
		{ "sand", [1f] },
		{ "snow", [1f] }
	};

	// start/stop/update
	public void Start()
	{
		_running = true;
		_workerThread = new Thread(WorkerLoop);
		_workerThread.Start();
	}

	public void Stop()
	{
		_running = false;
		_workerThread?.Join();
	}

	public void Update()
	{
		while (_finaliseQueue.TryDequeue(out var data)) FinaliseChunk(data);
	}

	public void RequestBuild(Vector2I coord)
	{
		_buildQueue.Enqueue(coord);
	}

	private void WorkerLoop()
	{
		while (_running)
			if (_buildQueue.TryDequeue(out var coord))
			{
				var result = BuildChunk(coord);
				_finaliseQueue.Enqueue(result);
			}
			else
			{
				Thread.Sleep(1);
			}
	}

	private Chunk BuildChunk(Vector2I chunkCoord)
	{
		var sw = System.Diagnostics.Stopwatch.StartNew();

		var c = world.ChunkSize;
		var tiles = new TileInstance[c, c];
		var objects = new List<ChunkObject>();
		var decors = new List<ChunkDecor>();
		var blocked = new bool[c, c];

		var baseBiomes = new BiomeDefinition[c, c];
		var finalBiomes = new BiomeDefinition[c, c];
		var waterFeatures = new WaterFeatureType[c, c];

		var chunk = new Chunk(chunkCoord, tiles, objects, decors, new Dictionary<string, ChunkTileMeshData>());
		world.TryGetChunkDelta(chunkCoord, out var chunkDelta);

		// pass 1, tile data
		for (var x = 0; x < c; x++)
		for (var y = 0; y < c; y++)
		{
			var globalX = chunkCoord.X * c + x;
			var globalY = chunkCoord.Y * c + y;

			// determine biome
			var sample = world.BiomeSampler.SampleTile(globalX, globalY);
			var baseBiome = sample.BaseBiome;
			var featureResult = sample.WaterFeature;
			var finalBiome = sample.FinalBiome;

			baseBiomes[x, y] = baseBiome;
			finalBiomes[x, y] = finalBiome;
			waterFeatures[x, y] = featureResult.Type;

			var tileDef = TileRegistry.Get(finalBiome.GroundTileId);
			tiles[x, y] = new TileInstance(tileDef, finalBiome.Id, sample.Temperature, sample.Humidity);
		}

		chunk.SpawnContext = new ChunkSpawnContext
		{
			BaseBiomes = baseBiomes,
			FinalBiomes = finalBiomes,
			WaterFeatures = waterFeatures
		};

		// pass 2, object data
		for (var x = 0; x < c; x++)
		for (var y = 0; y < c; y++)
		{
			var globalX = chunkCoord.X * c + x;
			var globalY = chunkCoord.Y * c + y;
			var tilePos = new Vector2I(globalX, globalY);

			var tileContext = new TileSpawnContext(
				x,
				y,
				globalX,
				globalY,
				chunk.SpawnContext.BaseBiomes[x, y],
				chunk.SpawnContext.FinalBiomes[x, y],
				chunk.SpawnContext.WaterFeatures[x, y]
			);

			// build procedural objects
			foreach (var spawn in finalBiomes[x, y].ObjectRules)
			{
				if (!TryGetEffectiveDensity(spawn, chunk.SpawnContext, tileContext, out var effectiveDensity))
					continue;

				if (!spawn.Algorithm.ShouldPlace(globalX, globalY, effectiveDensity))
					continue;

				// skip if removed in chunk delta
				if (chunkDelta != null && chunkDelta.RemovedProceduralObjects.Contains(tilePos))
					continue;

				if (blocked[x, y])
					continue;

				var variant = spawn.PickVariant(terrainSeed, globalX, globalY);
				var def = variant.Definition;

				var obj = new ChunkObject
				{
					Definition = def,
					TileCoord = tilePos,
					Position = new Vector3(globalX, 0, globalY),
					ChunkCoord = chunkCoord,
					Source = ChunkObjectSource.Procedural
				};

				objects.Add(obj);

				blocked[x, y] = obj.Definition.BlocksTile;
			}
		}

		// build player placed objects
		if (chunkDelta != null)
			foreach (var placed in chunkDelta.PlacedObjectsByTile.Values)
				objects.Add(new ChunkObject
				{
					Definition = WorldObjectRegistry.GetDefinition(placed.DefinitionTypeId),
					TileCoord = placed.TileCoord,
					Position = placed.Position,
					ChunkCoord = chunkCoord,
					Source = ChunkObjectSource.Placed
				});


		sw.Stop();
		chunk.BuildTimeMs = sw.Elapsed.TotalMilliseconds;
		return chunk;
	}

	private void FinaliseChunk(Chunk chunk)
	{
		var sw = System.Diagnostics.Stopwatch.StartNew();

		var chunkCoord = chunk.Coord;
		if (world.ActiveChunks.ContainsKey(chunkCoord))
			return;

		var c = world.ChunkSize;

		world.ActiveChunks[chunkCoord] = chunk;

		if (!chunkManager.InitialChunksReady &&
		    chunkManager.PendingInitialChunks.Remove(chunkCoord) &&
		    chunkManager.PendingInitialChunks.Count == 0)
		{
			chunkManager.InitialChunksReady = true;
			EmitSignal(SignalName.InitialChunksReady);
		}

		var pos = new Vector3I();
		var baseX = chunkCoord.X * c;
		var baseY = chunkCoord.Y * c;

		for (var x = 0; x < c; x++)
		for (var y = 0; y < c; y++)
		{
			var id = chunk.Tiles[x, y].Definition.GridTileId;
			pos.X = baseX + x;
			pos.Y = 0;
			pos.Z = baseY + y;

			if (chunk.Tiles[x, y].Definition.Name == "water")
				world.WaterMap.SetCellItem(pos, id);
			else
				world.GroundMap.SetCellItem(pos, id);
		}

		world.WorldObjectManager.EnqueueChunk(chunk);

		sw.Stop();
		chunk.FinaliseTimeMs = sw.Elapsed.TotalMilliseconds;

		GD.Print($"Chunk {chunkCoord} > Build {chunk.BuildTimeMs:F3}ms | Finalise {chunk.FinaliseTimeMs:F3}ms");
	}

	private int PickWeightedVariant(string tileType, int x, int y)
	{
		var seed = (ulong)HashCode.Combine(terrainSeed, x, y);
		_rng.Seed = seed;

		var index = _rng.RandWeighted(_tileVariantWeights[tileType]);
		return _tileVariants[tileType][(int)index];
	}

	private bool TryGetEffectiveDensity(
		ObjectSpawnRule rule,
		ChunkSpawnContext chunkCtx,
		in TileSpawnContext tileCtx,
		out float effectiveDensity)
	{
		effectiveDensity = 0f;

		var conditions = rule.Conditions;
		if (conditions == null)
		{
			effectiveDensity = rule.Density;
			return true;
		}

		var cache = new Dictionary<NeighborKey, int>();

		// Hard requirements
		foreach (var requirement in conditions.NeighbourRequirements)
		{
			var count = GetNeighbourCountCached(
				cache,
				chunkCtx,
				tileCtx.LocalX,
				tileCtx.LocalY,
				requirement.TargetType,
				requirement.TargetId,
				requirement.Radius);

			if (count < requirement.MinCount || count > requirement.MaxCount)
				return false;
		}

		// Soft density modifiers
		var multiplier = 1f;

		foreach (var modifier in conditions.DensityModifiers)
		{
			var count = GetNeighbourCountCached(
				cache,
				chunkCtx,
				tileCtx.LocalX,
				tileCtx.LocalY,
				modifier.TargetType,
				modifier.TargetId,
				modifier.Radius);

			float t;
			if (modifier.MaxCount <= modifier.MinCount)
				t = count >= modifier.MinCount ? 1f : 0f;
			else
				t = Mathf.Clamp(
					(count - modifier.MinCount) / (float)(modifier.MaxCount - modifier.MinCount),
					0f,
					1f);

			var localMultiplier = modifier.FalloffMode switch
			{
				DistanceFalloffMode.Linear => Mathf.Lerp(modifier.MinMultiplier, modifier.MaxMultiplier, t),
				_ => 1f
			};

			multiplier *= localMultiplier;
		}

		effectiveDensity = rule.Density * multiplier;
		return effectiveDensity > 0f;
	}

	private int CountMatchingNeighbours(ChunkSpawnContext chunkContext, int localX, int localY,
		NeighbourTargetType targetType, string targetId, int radius)
	{
		var count = 0;

		for (var dx = -radius; dx <= radius; dx++)
		for (var dy = -radius; dy <= radius; dy++)
		{
			if (dx == 0 && dy == 0)
				continue;

			var nx = localX + dx;
			var ny = localY + dy;

			if (nx < 0 || ny < 0 || nx >= world.ChunkSize || ny >= world.ChunkSize)
				continue;

			if (MatchesTarget(chunkContext, nx, ny, targetType, targetId))
				count++;
		}

		return count;
	}

	private bool MatchesTarget(ChunkSpawnContext chunkContext, int localX, int localY,
		NeighbourTargetType targetType, string targetId)
	{
		switch (targetType)
		{
			case NeighbourTargetType.WaterFeature:
				return chunkContext.WaterFeatures[localX, localY].ToString() == targetId;

			default:
				return false;
		}
	}

	private int GetNeighbourCountCached(
		Dictionary<NeighborKey, int> cache,
		ChunkSpawnContext chunkCtx,
		int localX,
		int localY,
		NeighbourTargetType targetType,
		string targetId,
		int radius)
	{
		var key = new NeighborKey(targetType, targetId, radius);

		if (cache.TryGetValue(key, out var count))
			return count;

		count = CountMatchingNeighbours(chunkCtx, localX, localY, targetType, targetId, radius);
		cache[key] = count;
		return count;
	}
}

public readonly record struct NeighborKey(
	NeighbourTargetType TargetType,
	string TargetId,
	int Radius
);