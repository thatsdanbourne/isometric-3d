using Godot;
using System;
using System.Collections.Concurrent;
using System.Threading;

public partial class ChunkGenerator : Node
{
	[Signal] public delegate void InitialChunksReadyEventHandler();

	private readonly World _world;
	private readonly ChunkManager _chunkManager;

	private Thread workerThread;
	private bool running;

	private readonly ConcurrentQueue<Vector2I> buildQueue = new();
	private readonly ConcurrentQueue<ChunkData> finaliseQueue = new();

	public ChunkGenerator(World world, ChunkManager chunkManager)
	{
		_world = world;
		_chunkManager = chunkManager;
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
				var result = BuildChunkData(coord);
				finaliseQueue.Enqueue(result);
			}
			else
			{
				Thread.Sleep(1);
			}
		}
	}

	private ChunkData BuildChunkData(Vector2I coord)
	{
		var sw = System.Diagnostics.Stopwatch.StartNew();

		int C = _world.ChunkSize;
		Tile[,] tiles = new Tile[C, C];
		var objects = new Godot.Collections.Array<ChunkObject>();
		var decors = new Godot.Collections.Array<ChunkDecor>();

		for (int x = 0; x < C; x++)
		{
			for (int y = 0; y < C; y++)
			{
				int globalX = coord.X * C + x;
				int globalY = coord.Y * C + y;

				float tempRaw = _world.TempNoise.GetNoise2D(globalX + _world.worldOffset.X, globalY + _world.worldOffset.Y);
				float humidityRaw = _world.HumidityNoise.GetNoise2D(globalX + _world.worldOffset.X, globalY + _world.worldOffset.Y);
				float riverVal = _world.RiverNoise.GetNoise2D(globalX, globalY + _world.worldOffset.Y);

				float temp = AdjustContrast((tempRaw + 1f) / 2f);
				float humidity = AdjustContrast((humidityRaw + 1f) / 2f);
				float riverDist = Math.Abs(riverVal);


				// determine biome
				var biome = RuleRegistry.GetBiome(temp, humidity);
				string tileType = biome.GroundTileType;
				int tileId = _world.GetTileId(tileType);

				bool biomeAllowsRivers = humidity > 0.45f;
				bool isRiver = riverDist < 0.05f && biomeAllowsRivers;

				if (isRiver)
				{
					tileType = "water";
					tileId = 0;
				}

				tiles[x, y] = new Tile(tileId, tileType, biome.Name, temp, humidity);


				if (!isRiver)
				{
					// build objects
					foreach (ObjectSpawnRule spawn in biome.ObjectRules)
					{
						if (spawn.Algorithm.ShouldPlace(globalX, globalY, spawn.Density))
						{
							var obj = new ChunkObject();
							obj.ObjectRule = spawn;
							obj.Position = new Vector3(globalX + 0.25f, 0, globalY + 0.25f);
							objects.Add(obj);
						}
					}

					foreach (DecorSpawnRule decorRule in biome.DecorRules)
					{
						if (decorRule.ShouldPlace(globalX, globalY))
						{
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
		var result = new ChunkData(coord, tiles, objects, decors);
		result.BuildTimeMs = sw.Elapsed.TotalMilliseconds;
		return result;
	}

	private float AdjustContrast(float v)
	{
		float contrast = 1.4f;
		return Mathf.Clamp((v - 0.5f) * contrast + 0.5f, 0f, 1f);
	}

	private void FinaliseChunk(ChunkData data)
	{
		var sw = System.Diagnostics.Stopwatch.StartNew();

		Vector2I coord = data.Coord;
		if (_world.ActiveChunks.ContainsKey(coord))
			return;

		int C = _world.ChunkSize;

		Chunk chunk = new Chunk(C)
		{
			Coord = coord,
			Tiles = data.Tiles
		};

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
				int id = data.Tiles[x, y].Id;
				pos.X = baseX + x;
				pos.Y = 0;
				pos.Z = baseY + y;

				if (data.Tiles[x, y].Type == "water")
				{
					_world.WaterMap.SetCellItem(pos, id);
				}
				else
				{
					_world.GroundMap.SetCellItem(pos, id);
				}
			}
		}

		foreach (ChunkObject obj in data.Objects)
		{
			var rule = obj.ObjectRule;
			var AllowedVariants = rule.Variants;

			var variant = _world.PickObjectVariant(rule, AllowedVariants, (int)obj.Position.X, (int)obj.Position.Z);
			var scene = variant.Scene;
			var instance = scene.Instantiate<Node3D>();

			if (instance is WorldObject wo)
			{
				wo.World = _world;
				wo.Chunk = chunk;
			}

			if (instance.HasMethod("initialise"))
				instance.Call("initialise");

			instance.Position = obj.Position;
			_world.WorldObjects.AddChild(instance);
			chunk.Objects.Add(instance);
		}

		foreach (ChunkDecor decor in data.Decors)
		{
			var scene = WorldObjectRegistry.GetScene(decor.DecorRule.DecorId);
			var instance = scene.Instantiate<Node3D>();
			instance.Position = decor.Position;
			_world.WorldObjects.AddChild(instance);
			chunk.Decors.Add(instance);
		}

		sw.Stop();
		data.FinaliseTimeMs = sw.Elapsed.TotalMilliseconds;

		GD.Print($"Chunk {coord} > Build {data.BuildTimeMs:F3}ms | Finalise {data.FinaliseTimeMs:F3}ms");
	}
}
