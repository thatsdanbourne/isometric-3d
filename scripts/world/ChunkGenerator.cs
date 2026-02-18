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

	private Chunk BuildChunk(Vector2I coord)
	{
		var sw = System.Diagnostics.Stopwatch.StartNew();

		var c = world.ChunkSize;
		var tiles = new TileInstance[c, c];
		var objects = new List<ChunkObject>();
		var decors = new List<ChunkDecor>();
		var blocked = new bool[c, c];

		var chunk = new Chunk(coord, tiles, objects, decors, new Dictionary<string, ChunkTileMeshData>());
		world.TryGetChunkDelta(coord, out var chunkDelta);

		for (var x = 0; x < c; x++)
		for (var y = 0; y < c; y++)
		{
			var globalX = coord.X * c + x;
			var globalY = coord.Y * c + y;
			var tilePos = new Vector2I(globalX, globalY);

			var tempRaw = world.TempNoise.GetNoise2D(globalX + world.WorldOffset.X, globalY + world.WorldOffset.Y);
			var humidityRaw =
				world.HumidityNoise.GetNoise2D(globalX + world.WorldOffset.X, globalY + world.WorldOffset.Y);
			var riverVal = world.RiverNoise.GetNoise2D(globalX, globalY + world.WorldOffset.Y);

			var temp = AdjustContrast((tempRaw + 1f) / 2f);
			var humidity = AdjustContrast((humidityRaw + 1f) / 2f);
			var riverDist = Math.Abs(riverVal);


			// determine biome
			var biome = RuleRegistry.GetBiome(temp, humidity);
			// int tileId = PickWeightedVariant(biome.GroundTileType, globalX, globalY);
			var tileDef = TileRegistry.GetByName(biome.GroundTileType);

			var biomeAllowsRivers = humidity > 0.45f;
			var isRiver = riverDist < 0.035f && biomeAllowsRivers;
			var isRiverBank = riverDist < 0.085f && biomeAllowsRivers;

			if (isRiver)
				tileDef = TileRegistry.GetByName("water");
			else if (isRiverBank) tileDef = TileRegistry.GetByName("sand");

			tiles[x, y] = new TileInstance(tileDef, biome.Name, temp, humidity);

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
			if (!isRiver)
				// build procedural objects
				foreach (var spawn in biome.ObjectRules)
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
							TileCoord = new Vector2I(globalX, globalY),
							Position = new Vector3(globalX, 0, globalY),
							ChunkCoord = coord,
							Source = ChunkObjectSource.Procedural
						};

						objects.Add(obj);

						blocked[x, y] = obj.Definition.BlocksTile;
					}

			// foreach (var decorRule in biome.DecorRules)
			//     if (decorRule.ShouldPlace(globalX, globalY))
			//     {
			//         if (blocked[x, y])
			//             continue;
			//         var dec = new ChunkDecor();
			//         dec.DecorRule = decorRule;
			//         dec.Position = new Vector3(globalX + 0.25f, 0, globalY + 0.25f);
			//         decors.Add(dec);
			//     }
		}

		// build player placed objects
		if (chunkDelta != null)
			foreach (var placed in chunkDelta.PlacedObjects)
				objects.Add(placed);

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

		var coord = chunk.Coord;
		if (world.ActiveChunks.ContainsKey(coord))
			return;

		var c = world.ChunkSize;

		world.ActiveChunks[coord] = chunk;

		if (!chunkManager.InitialChunksReady &&
		    chunkManager.PendingInitialChunks.Remove(coord) &&
		    chunkManager.PendingInitialChunks.Count == 0)
		{
			chunkManager.InitialChunksReady = true;
			EmitSignal(SignalName.InitialChunksReady);
		}

		var pos = new Vector3I();
		var baseX = coord.X * c;
		var baseY = coord.Y * c;

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

		// foreach (var decor in chunk.Decors)
		// {
		//     var scene = WorldObjectRegistry.GetDefinition(decor.DecorRule.DecorId).Scene;
		//     var instance = scene.Instantiate<WorldDecor>();
		//     instance.Position = decor.Position;
		//     _world.WorldObjects.AddChild(instance);
		// }

		world.WorldObjectManager.EnqueueChunk(chunk);

		sw.Stop();
		chunk.FinaliseTimeMs = sw.Elapsed.TotalMilliseconds;

		GD.Print($"Chunk {coord} > Build {chunk.BuildTimeMs:F3}ms | Finalise {chunk.FinaliseTimeMs:F3}ms");
	}

	private int PickWeightedVariant(string tileType, int x, int y)
	{
		var seed = (ulong)HashCode.Combine(terrainSeed, x, y);
		_rng.Seed = seed;

		var index = _rng.RandWeighted(_tileVariantWeights[tileType]);
		return _tileVariants[tileType][(int)index];
	}
}