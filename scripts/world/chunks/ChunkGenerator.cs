using Godot;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

public partial class ChunkGenerator(World world, int terrainSeed) : Node
{
	[Signal]
	public delegate void InitialChunksReadyEventHandler();

	private Thread _workerThread;
	private bool _running;

	private readonly ConcurrentQueue<Vector2I> _buildQueue = new();
	private readonly ConcurrentQueue<Chunk> _builtChunkQueue = new();
	private readonly Queue<Chunk> _clientChunkQueue = new();
	private RandomNumberGenerator _rng = new();

	private const int MaxClientChunksFinalisedPerFrame = 1;

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

	public void ProcessBuiltChunks()
	{
		while (_builtChunkQueue.TryDequeue(out var chunk))
		{
			world.ServerChunks[chunk.Coord] = chunk;
			world.ChunkManager.OnServerChunkBuilt(chunk.Coord);
		}
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
				_builtChunkQueue.Enqueue(result);
			}
			else
			{
				Thread.Sleep(1);
			}
	}

	private Chunk BuildChunk(Vector2I chunkCoord)
	{
		var c = world.ChunkSize;
		var tiles = new TileInstance[c, c];
		var objects = new List<ChunkObject>();
		var blocked = new bool[c, c];

		var baseBiomes = new BiomeDefinition[c, c];
		var finalBiomes = new BiomeDefinition[c, c];
		var waterFeatures = new WaterFeatureType[c, c];

		world.TryGetChunkDelta(chunkCoord, out var chunkDelta);

		// pass 1, tile data
		for (var x = 0; x < c; x++)
		for (var y = 0; y < c; y++)
		{
			var globalX = chunkCoord.X * c + x;
			var globalY = chunkCoord.Y * c + y;

			// determine biome
			var sample = world.BiomeSampler.SampleTile(globalX, globalY);

			baseBiomes[x, y] = sample.BaseBiome;
			finalBiomes[x, y] = sample.FinalBiome;
			waterFeatures[x, y] = sample.WaterFeature.Type;

			var tileDef = TileRegistry.Get(sample.FinalBiome.GroundTileId);
			tiles[x, y] = new TileInstance(
				tileDef,
				sample.FinalBiome.Id,
				sample.Temperature,
				sample.Humidity
			);
		}

		var spawnContext = new ChunkSpawnContext
		{
			BaseBiomes = baseBiomes,
			FinalBiomes = finalBiomes,
			WaterFeatures = waterFeatures,
			Objects = new int[c, c]
		};

		// pass 2, structure data
		BuildStructuresForChunk(chunkCoord, objects, blocked, spawnContext, chunkDelta);

		// pass 3, object data
		var objectSpawnPasses = new[]
		{
			ObjectSpawnPass.LargeObjects,
			ObjectSpawnPass.GroundPickups,
			ObjectSpawnPass.Decor
		};

		foreach (var pass in objectSpawnPasses)
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
					baseBiomes[x, y],
					finalBiomes[x, y],
					waterFeatures[x, y]
				);

				// build procedural objects
				foreach (var spawn in finalBiomes[x, y].ObjectRules)
				{
					if (spawn.Pass != pass)
						continue;

					if (!TryGetEffectiveDensity(spawn, spawnContext, tileContext, out var effectiveDensity))
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

					var rotHash = DeterministicHash.CombineU32(terrainSeed, spawn.StableId, globalX, globalY,
						variant.StableId);
					var rot = rotHash / (float)uint.MaxValue * Mathf.Tau;

					objects.Add(new ChunkObject
					{
						Definition = def,
						TileCoord = tilePos,
						Position = new Vector3(globalX, 0, globalY),
						Rotation = rot,
						ChunkCoord = chunkCoord,
						Source = ChunkObjectSource.Procedural
					});

					spawnContext.Objects[x, y] = def.StableId;

					if (def.BlocksTile)
						blocked[x, y] = true;
				}
			}

		// build player-placed objects
		if (chunkDelta != null)
			foreach (var placed in chunkDelta.PlacedObjectsByTile.Values)
			{
				var def = WorldObjectRegistry.GetDefinition(placed.DefinitionTypeId);
				objects.Add(new ChunkObject
				{
					Definition = def,
					TileCoord = placed.TileCoord,
					Position = placed.Position,
					ChunkCoord = chunkCoord,
					Source = ChunkObjectSource.Placed
				});
			}

		var chunk = new Chunk(chunkCoord, tiles, objects);

		if (chunkDelta != null)
		{
			foreach (var kv in chunkDelta.StorageStates)
				chunk.StorageStates[kv.Key] = kv.Value;

			foreach (var kv in chunkDelta.StationStates)
				chunk.StationStates[kv.Key] = kv.Value;
		}

		return chunk;
	}

	public void EnqueueClientChunk(Chunk chunk)
	{
		if (world.ActiveChunks.ContainsKey(chunk.Coord))
			return;

		_clientChunkQueue.Enqueue(chunk);
	}

	public void ProcessClientChunkQueue()
	{
		var count = 0;

		while (_clientChunkQueue.TryDequeue(out var chunk))
		{
			FinaliseChunk(chunk);
			count++;

			if (count >= MaxClientChunksFinalisedPerFrame)
				break;
		}
	}

	public void FinaliseChunk(Chunk chunk)
	{
		var chunkCoord = chunk.Coord;
		if (world.ActiveChunks.ContainsKey(chunkCoord))
			return;

		var c = world.ChunkSize;
		var pos = new Vector3I();
		var baseX = chunkCoord.X * c;
		var baseY = chunkCoord.Y * c;

		for (var x = 0; x < c; x++)
		for (var y = 0; y < c; y++)
		{
			var tile = chunk.Tiles[x, y];
			var id = tile.Definition.GridTileId;

			pos.X = baseX + x;
			pos.Y = 0;
			pos.Z = baseY + y;

			if (tile.Definition.Name == "water")
				world.WaterMap.SetCellItem(pos, id);
			else
				world.GroundMap.SetCellItem(pos, id);
		}

		world.WorldObjectManager.EnqueueChunk(chunk);
		world.ActiveChunks[chunkCoord] = chunk;
	}

	private void BuildStructuresForChunk(Vector2I chunkCoord, List<ChunkObject> objects, bool[,] blocked,
		ChunkSpawnContext spawnContext, ChunkDeltaData chunkDelta)
	{
		var c = world.ChunkSize;

		var minTile = new Vector2I(chunkCoord.X * c, chunkCoord.Y * c);
		var maxTile = minTile + new Vector2I(c - 1, c - 1);

		// Need to check nearby structure regions because a structure
		// can start outside this chunk but overlap into it.
		const int regionSize = 64;

		var minRegion = new Vector2I(Mathf.FloorToInt(minTile.X / (float)regionSize) - 1,
			Mathf.FloorToInt(minTile.Y / (float)regionSize) - 1);

		var maxRegion = new Vector2I(Mathf.FloorToInt(maxTile.X / (float)regionSize) + 1,
			Mathf.FloorToInt(maxTile.Y / (float)regionSize) + 1);

		for (var rx = minRegion.X; rx <= maxRegion.X; rx++)
		for (var ry = minRegion.Y; ry <= maxRegion.Y; ry++)
			TryBuildStructureFromRegion(chunkCoord, new Vector2I(rx, ry), regionSize, objects, blocked, spawnContext,
				chunkDelta);
	}

	private void TryBuildStructureFromRegion(
		Vector2I chunkCoord,
		Vector2I regionCoord,
		int regionSize,
		List<ChunkObject> objects,
		bool[,] blocked,
		ChunkSpawnContext spawnContext,
		ChunkDeltaData chunkDelta
	)
	{
		var c = world.ChunkSize;

		var hash = DeterministicHash.CombineU32(
			terrainSeed,
			regionCoord.X,
			regionCoord.Y,
			918273
		);

		var rng = new RandomNumberGenerator();
		rng.Seed = hash;

		var originTile = new Vector2I(
			regionCoord.X * regionSize + rng.RandiRange(0, regionSize - 1),
			regionCoord.Y * regionSize + rng.RandiRange(0, regionSize - 1)
		);

		var biomeDef = world.BiomeSampler.GetFinalBiomeAtTile(originTile.X, originTile.Y);

		if (biomeDef.StructureRules.Count == 0)
			return;

		foreach (var rule in biomeDef.StructureRules)
		{
			if (rng.Randf() > rule.Density)
				continue;

			var structureId = PickStructureVariant(rule, rng);
			var structure = StructureRegistry.Get(structureId);

			EmitStructureToChunk(
				chunkCoord,
				originTile,
				structure,
				objects,
				blocked,
				spawnContext,
				chunkDelta
			);

			return;
		}
	}

	private static string PickStructureVariant(
		StructureSpawnRule rule,
		RandomNumberGenerator rng
	)
	{
		var total = 0f;

		foreach (var variant in rule.Variants)
			total += variant.Weight;

		var roll = rng.Randf() * total;

		foreach (var variant in rule.Variants)
		{
			if (roll <= variant.Weight)
				return variant.Id;

			roll -= variant.Weight;
		}

		return rule.Variants[0].Id;
	}

	private void EmitStructureToChunk(
		Vector2I chunkCoord,
		Vector2I originTile,
		StructureDefinition structure,
		List<ChunkObject> objects,
		bool[,] blocked,
		ChunkSpawnContext spawnContext,
		ChunkDeltaData chunkDelta
	)
	{
		var c = world.ChunkSize;

		foreach (var part in structure.Objects)
		{
			var tilePos = originTile + part.LocalTile;

			if (TileUtils.TileToChunk(tilePos) != chunkCoord)
				continue;

			if (chunkDelta != null &&
			    chunkDelta.RemovedProceduralObjects.Contains(tilePos))
				continue;

			var localX = tilePos.X - chunkCoord.X * c;
			var localY = tilePos.Y - chunkCoord.Y * c;

			if (localX < 0 || localX >= c || localY < 0 || localY >= c)
				continue;

			if (blocked[localX, localY])
				continue;


			var chanceHash = DeterministicHash.CombineU32(
				terrainSeed,
				structure.StableId,
				originTile.X,
				originTile.Y,
				part.LocalTile.X,
				part.LocalTile.Y,
				12345
			);

			var chanceRoll = chanceHash / (float)uint.MaxValue;

			if (chanceRoll > part.Chance)
				continue;

			var variant = part.PickVariant(terrainSeed, structure.StableId, originTile);
			var def = variant.Definition;

			objects.Add(new ChunkObject
			{
				Definition = def,
				TileCoord = tilePos,
				Position = new Vector3(tilePos.X, 0, tilePos.Y),
				ChunkCoord = chunkCoord,
				Source = ChunkObjectSource.Procedural
			});

			spawnContext.Objects[localX, localY] = def.StableId;

			if (def.BlocksTile)
				blocked[localX, localY] = true;
		}
	}

	private bool TryGetEffectiveDensity(
		ObjectSpawnRule rule,
		ChunkSpawnContext chunkCtx,
		in TileSpawnContext tileCtx,
		out float effectiveDensity)
	{
		var conditions = rule.Conditions;

		if (conditions == null)
		{
			effectiveDensity = rule.Density;
			return effectiveDensity > 0f;
		}

		var cache = new Dictionary<NeighborKey, int>();

		if (!MeetsNeighbourRequirements(conditions, chunkCtx, tileCtx, cache))
		{
			effectiveDensity = 0f;
			return false;
		}

		var multiplier = GetDensityMultiplier(conditions, chunkCtx, tileCtx, cache);

		effectiveDensity = rule.Density * multiplier;
		return effectiveDensity > 0f;
	}

	private bool MeetsNeighbourRequirements(
		SpawnConditions conditions,
		ChunkSpawnContext chunkCtx,
		in TileSpawnContext tileCtx,
		Dictionary<NeighborKey, int> cache)
	{
		foreach (var req in conditions.NeighbourRequirements)
		{
			var count = GetNeighbourCountCached(
				cache,
				chunkCtx,
				tileCtx.LocalX,
				tileCtx.LocalY,
				req.TargetType,
				req.TargetId,
				req.Radius);

			if (count < req.MinCount || count > req.MaxCount)
				return false;
		}

		return true;
	}

	private float GetDensityMultiplier(
		SpawnConditions conditions,
		ChunkSpawnContext chunkCtx,
		in TileSpawnContext tileCtx,
		Dictionary<NeighborKey, int> cache)
	{
		var multiplier = 1f;

		foreach (var mod in conditions.DensityModifiers)
		{
			var count = GetNeighbourCountCached(
				cache,
				chunkCtx,
				tileCtx.LocalX,
				tileCtx.LocalY,
				mod.TargetType,
				mod.TargetId,
				mod.Radius);

			var t = GetModifierT(count, mod.MinCount, mod.MaxCount);

			var localMultiplier = mod.FalloffMode switch
			{
				DistanceFalloffMode.Linear =>
					Mathf.Lerp(mod.MinMultiplier, mod.MaxMultiplier, t),
				_ => 1f
			};

			multiplier *= localMultiplier;
		}

		return multiplier;
	}

	private static float GetModifierT(int count, int minCount, int maxCount)
	{
		if (count < minCount)
			return 0f;

		if (maxCount <= minCount)
			return 1f;

		return Mathf.Clamp(
			(count - minCount) / (float)(maxCount - minCount),
			0f,
			1f);
	}

	private int CountMatchingNeighbours(ChunkSpawnContext chunkContext, int localX, int localY,
		NeighbourTargetType targetType, int targetId, int radius)
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
		NeighbourTargetType targetType, int targetId)
	{
		return targetType switch
		{
			NeighbourTargetType.WaterFeature =>
				(int)chunkContext.WaterFeatures[localX, localY] == targetId,

			NeighbourTargetType.Object =>
				chunkContext.Objects[localX, localY] == targetId,

			_ => false
		};
	}

	private int GetNeighbourCountCached(
		Dictionary<NeighborKey, int> cache,
		ChunkSpawnContext chunkCtx,
		int localX,
		int localY,
		NeighbourTargetType targetType,
		int targetId,
		int radius)
	{
		var key = new NeighborKey(localX, localY, targetType, targetId, radius);

		if (cache.TryGetValue(key, out var count))
			return count;

		count = CountMatchingNeighbours(chunkCtx, localX, localY, targetType, targetId, radius);
		cache[key] = count;
		return count;
	}
}

public readonly record struct NeighborKey(
	int LocalX,
	int LocalY,
	NeighbourTargetType TargetType,
	int TargetId,
	int Radius
);