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
		var mobs = new List<ChunkMob>();
		var blocked = new bool[c, c];

		var chunk = new Chunk(chunkCoord, tiles, objects, decors, mobs, new Dictionary<string, ChunkTileMeshData>());
		world.TryGetChunkDelta(chunkCoord, out var chunkDelta);

		for (var x = 0; x < c; x++)
		for (var y = 0; y < c; y++)
		{
			var globalX = chunkCoord.X * c + x;
			var globalY = chunkCoord.Y * c + y;
			var tilePos = new Vector2I(globalX, globalY);

			var tempRaw = world.TempNoise.GetNoise2D(globalX + world.WorldOffset.X, globalY + world.WorldOffset.Y);
			var humidityRaw =
				world.HumidityNoise.GetNoise2D(globalX + world.WorldOffset.X, globalY + world.WorldOffset.Y);
			var riverVal = world.RiverNoise.GetNoise2D(globalX, globalY + world.WorldOffset.Y);

			var temp = AdjustContrast((tempRaw + 1f) / 2f);
			var humidity = AdjustContrast((humidityRaw + 1f) / 2f);
			var riverDist = Math.Abs(riverVal);


			// determine biome
			var baseBiome = RuleRegistry.GetBiome(temp, humidity);
			var featureResult = ResolveWaterFeature(globalX, globalY, humidity, baseBiome);
			var finalBiome = featureResult.BiomeOverride ?? baseBiome;
			// int tileId = PickWeightedVariant(biome.GroundTileType, globalX, globalY);
			var tileDef = TileRegistry.Get(finalBiome.GroundTileId);

			tiles[x, y] = new TileInstance(tileDef, finalBiome.Id, temp, humidity);

			// foreach (var rule in tileDef.DetailMeshes)
			// {
			// 	if (rng.Randf() > rule.Density)
			// 		continue;
			// 	int count = rng.RandiRange(rule.MinPerTile, rule.MaxPerTile);
			// 	var meshData = chunk.GetOrCreateDetailMesh(rule.MeshId);
			// 	for (int i = 0; i < count; i++)
			// 	{
			// 		Transform3D t = Transform3D.Identity;
			// 		Vector3 basePos = TileManager.TileToWorld(tilePos);
			// 		basePos.X += rng.RandfRange(-0.5f, 0.5f);
			// 		basePos.Z += rng.RandfRange(-0.5f, 0.5f);
			// 		t.Origin = basePos;
			// 		meshData.Transforms.Add(t);
			// 	}
			// }

			// build procedural objects
			foreach (var spawn in finalBiome.ObjectRules)
				if (spawn.Algorithm.ShouldPlace(globalX, globalY, spawn.Density))
				{
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

	private float AdjustContrast(float v)
	{
		var contrast = 1.4f;
		return Mathf.Clamp((v - 0.5f) * contrast + 0.5f, 0f, 1f);
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

		// foreach (var meshData in chunk.DetailMeshes)
		// {
		// 	var mmi = new MultiMeshInstance3D();
		// 	var mm = new MultiMesh();

		// 	mm.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
		// 	mm.Mesh = meshData.Value.Mesh;
		// 	mm.InstanceCount = meshData.Value.Transforms.Count;

		// 	for (int i = 0; i < mm.InstanceCount; i++)
		// 	{
		// 		mm.SetInstanceTransform(i, meshData.Value.Transforms[i]);
		// 	}

		// 	meshData.Value.Mesh.SurfaceSetMaterial(0, meshData.Value.Material);
		// 	mmi.Multimesh = mm;
		// 	mmi.Name = meshData.Value.MeshId;
		// 	_world.WorldObjects.AddChild(mmi);
		// }

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

	private WaterFeatureResult ResolveWaterFeature(
		int globalX,
		int globalY,
		float humidity,
		BiomeDefinition baseBiome)
	{
		float x = globalX + world.WorldOffset.X;
		float y = globalY + world.WorldOffset.Y;

		var biomeAllowsRivers = humidity > 0.45f;

		if (!biomeAllowsRivers)
			return new WaterFeatureResult(WaterFeatureType.None, null);

		var riverRaw = world.RiverNoise.GetNoise2D(x, y);
		var lakeRaw = world.LakeNoise.GetNoise2D(x, y);
		var drainageRaw = world.DrainageNoise.GetNoise2D(x, y);
		var bankRaw = world.BankNoise.GetNoise2D(x, y);

		var riverLine = Math.Abs(riverRaw);
		var drainage = (drainageRaw + 1f) * 0.5f;
		var lake = (lakeRaw + 1f) * 0.5f;

		var drainageMask = Mathf.SmoothStep(0.45f, 0.75f, drainage);
		var lakeMask = lake * humidity;

		var isLake = lakeMask > 0.34f;

		var bankJitter = bankRaw * 0.006f;

		var baseRiverWidth = 0.018f;
		var drainageWidth = drainageMask * 0.018f;
		var lakeWidthBoost = lakeMask > 0.26 ? 0.02f : 0f;

		var riverWidth = baseRiverWidth + drainageMask * 0.015f + (lakeMask > 0.72f ? 0.02f : 0f) + bankJitter;
		var riverBankWidth = riverWidth + 0.045f;

		var riverAllowed = humidity > 0.45f && drainageMask > 0.15f;

		var isRiver = riverAllowed && riverLine < riverWidth;
		var isRiverBank = riverAllowed && !isLake && riverLine < riverBankWidth;

		if (isLake)
			return new WaterFeatureResult(
				WaterFeatureType.Lake,
				RuleRegistry.GetBiomeById(BiomeId.Lake));


		if (isRiver)
			return new WaterFeatureResult(
				WaterFeatureType.River,
				RuleRegistry.GetBiomeById(BiomeId.River));

		if (isRiverBank)
			return new WaterFeatureResult(
				WaterFeatureType.RiverBank,
				RuleRegistry.GetBiomeById(BiomeId.Riverbank));

		return new WaterFeatureResult(WaterFeatureType.None, null);
	}
}