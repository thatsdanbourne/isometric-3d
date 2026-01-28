using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

public partial class ChunkGenerator : Node
{
	[Signal] public delegate void InitialChunksReadyEventHandler();

	private readonly World _world;
	private readonly ChunkManager _chunkManager;
	private readonly int terrainSeed;

	private Thread workerThread;
	private bool running;

	private readonly ConcurrentQueue<Vector2I> buildQueue = new();
	private readonly ConcurrentQueue<Chunk> finaliseQueue = new();
	private RandomNumberGenerator rng = new();

	private Dictionary<string, int[]> tileVariants = new()
	{
		{ "grass", new[] { 0, 1 } },
		{ "sand", new[] { 3 } },
		{ "snow", new[] { 4 } },
	};

	private Dictionary<string, float[]> tileVariantWeights = new()
	{
		{ "grass", new[] { 0.795f, 0.005f } },
		{ "sand", new[] { 1f } },
		{ "snow", new[] { 1f } },
	};


	public ChunkGenerator(World world, ChunkManager chunkManager, int seed)
	{
		_world = world;
		_chunkManager = chunkManager;
		this.terrainSeed = seed;
	}

	// start/stop/update
	public void Start()
	{
		running = true;
		workerThread = new Thread(WorkerLoop);
		workerThread.Start();
	}

	public void Stop()
	{
		running = false;
		workerThread?.Join();
	}

	public void Update()
	{
		while (finaliseQueue.TryDequeue(out var data))
		{
			FinaliseChunk(data);
		}
	}

	public void RequestBuild(Vector2I coord)
	{
		buildQueue.Enqueue(coord);
	}

	private void WorkerLoop()
	{
		while (running)
		{
			if (buildQueue.TryDequeue(out Vector2I coord))
			{
				var result = BuildChunk(coord);
				finaliseQueue.Enqueue(result);
			}
			else
			{
				Thread.Sleep(1);
			}
		}
	}

	private Chunk BuildChunk(Vector2I coord)
	{
		var sw = System.Diagnostics.Stopwatch.StartNew();

		int C = _world.ChunkSize;
		TileInstance[,] tiles = new TileInstance[C, C];
		var objects = new List<ChunkObject>();
		var decors = new List<ChunkDecor>();
		bool[,] blocked = new bool[C, C];

		Chunk chunk = new Chunk(coord, tiles, objects, decors, new());

		for (int x = 0; x < C; x++)
		{
			for (int y = 0; y < C; y++)
			{
				int globalX = coord.X * C + x;
				int globalY = coord.Y * C + y;
				Vector2I tilePos = new Vector2I(globalX, globalY);

				float tempRaw = _world.TempNoise.GetNoise2D(globalX + _world.worldOffset.X, globalY + _world.worldOffset.Y);
				float humidityRaw = _world.HumidityNoise.GetNoise2D(globalX + _world.worldOffset.X, globalY + _world.worldOffset.Y);
				float riverVal = _world.RiverNoise.GetNoise2D(globalX, globalY + _world.worldOffset.Y);

				float temp = AdjustContrast((tempRaw + 1f) / 2f);
				float humidity = AdjustContrast((humidityRaw + 1f) / 2f);
				float riverDist = Math.Abs(riverVal);


				// determine biome
				var biome = RuleRegistry.GetBiome(temp, humidity);
				// int tileId = PickWeightedVariant(biome.GroundTileType, globalX, globalY);
				TileDefinition tileDef = TileRegistry.GetByName(biome.GroundTileType);

				bool biomeAllowsRivers = humidity > 0.45f;
				bool isRiver = riverDist < 0.035f && biomeAllowsRivers;
				bool isRiverBank = riverDist < 0.085f && biomeAllowsRivers;

				if (isRiver)
				{
					tileDef = TileRegistry.GetByName("water");
				}
				else if (isRiverBank)
				{
					tileDef = TileRegistry.GetByName("sand");
				}

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
				{
					// build objects
					foreach (ObjectSpawnRule spawn in biome.ObjectRules)
					{
						if (spawn.Algorithm.ShouldPlace(globalX, globalY, spawn.Density))
						{
							if (blocked[x, y])
								continue;

							var variant = spawn.PickVariant(terrainSeed, globalX, globalY);
							var def = variant.Definition;

							var obj = new ChunkObject()
							{
								Definition = def,
								TileCoord = new Vector2I(globalX, globalY),
								Position = new Vector3(globalX + 0.25f, 0, globalY + 0.25f),
								ChunkCoord = coord,
							};

							objects.Add(obj);

							if (def.BlocksTile)
								blocked[x, y] = true;
						}
					}

					foreach (DecorSpawnRule decorRule in biome.DecorRules)
					{
						if (decorRule.ShouldPlace(globalX, globalY))
						{
							if (blocked[x, y])
								continue;

							var dec = new ChunkDecor();
							dec.DecorRule = decorRule;
							dec.Position = new Vector3(globalX + 0.25f, 0, globalY + 0.25f);
							decors.Add(dec);
						}
					}
				}
			}
		}

		sw.Stop();
		chunk.BuildTimeMs = sw.Elapsed.TotalMilliseconds;
		return chunk;
	}

	private float AdjustContrast(float v)
	{
		float contrast = 1.4f;
		return Mathf.Clamp((v - 0.5f) * contrast + 0.5f, 0f, 1f);
	}

	private void FinaliseChunk(Chunk chunk)
	{
		var sw = System.Diagnostics.Stopwatch.StartNew();

		Vector2I coord = chunk.Coord;
		if (_world.ActiveChunks.ContainsKey(coord))
			return;

		int C = _world.ChunkSize;

		_world.ActiveChunks[coord] = chunk;

		if (!_chunkManager.InitialChunksReady &&
			_chunkManager.PendingInitialChunks.Remove(coord) &&
			_chunkManager.PendingInitialChunks.Count == 0)
		{
			_chunkManager.InitialChunksReady = true;
			EmitSignal(SignalName.InitialChunksReady);
		}

		Vector3I pos = new Vector3I();
		int baseX = coord.X * C;
		int baseY = coord.Y * C;

		for (int x = 0; x < C; x++)
		{
			for (int y = 0; y < C; y++)
			{
				int id = chunk.Tiles[x, y].Definition.GridTileId;
				pos.X = baseX + x;
				pos.Y = 0;
				pos.Z = baseY + y;

				if (chunk.Tiles[x, y].Definition.Name == "water")
				{
					_world.WaterMap.SetCellItem(pos, id);
				}
				else
				{
					_world.GroundMap.SetCellItem(pos, id);
				}
			}
		}

		foreach (var meshData in chunk.DetailMeshes)
		{
			var mmi = new MultiMeshInstance3D();
			var mm = new MultiMesh();

			mm.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
			mm.Mesh = meshData.Value.Mesh;
			mm.InstanceCount = meshData.Value.Transforms.Count;

			for (int i = 0; i < mm.InstanceCount; i++)
			{
				mm.SetInstanceTransform(i, meshData.Value.Transforms[i]);
			}

			meshData.Value.Mesh.SurfaceSetMaterial(0, meshData.Value.Material);
			mmi.Multimesh = mm;
			mmi.Name = meshData.Value.MeshId;
			_world.WorldObjects.AddChild(mmi);
		}

		// foreach (ChunkDecor decor in chunk.Decors)
		// {
		// 	var scene = WorldObjectRegistry.GetDefinition(decor.DecorRule.DecorId).Scene;
		// 	var instance = scene.Instantiate<WorldDecor>();
		// 	instance.Position = decor.Position;
		// 	_world.WorldObjects.AddChild(instance);
		// }

		_world.WorldObjectManager.EnqueueChunk(chunk);

		sw.Stop();
		chunk.FinaliseTimeMs = sw.Elapsed.TotalMilliseconds;

		GD.Print($"Chunk {coord} > Build {chunk.BuildTimeMs:F3}ms | Finalise {chunk.FinaliseTimeMs:F3}ms");
	}

	private int PickWeightedVariant(string tileType, int x, int y)
	{
		ulong seed = (ulong)HashCode.Combine(terrainSeed, x, y);
		rng.Seed = seed;

		long index = rng.RandWeighted(tileVariantWeights[tileType]);
		return tileVariants[tileType][(int)index];
	}
}
