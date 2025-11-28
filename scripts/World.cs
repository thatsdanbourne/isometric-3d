using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

public partial class World : Node3D
{
	[Export] public Node3D WorldObjects;
	[Export] public GridMap GroundMap;
	[Export] public GridMap WaterMap;
	[Export] public Node3D Player;

	public int ChunkSize = 16;
	public int ChunkRadius = 3;

	public FastNoiseLite TempNoise;
	public FastNoiseLite HumidityNoise;
	public FastNoiseLite RiverNoise;

	private RuleRegistry ruleRegistry;

	public readonly System.Collections.Generic.Dictionary<Vector2I, Chunk> ActiveChunks
		= new System.Collections.Generic.Dictionary<Vector2I, Chunk>();
	
	private Thread workerThread;
	private bool running = true;

	private readonly ConcurrentQueue<Vector2I> buildQueue = new();
	private readonly ConcurrentQueue<ChunkData> finaliseQueue = new();

	private Vector2I lastPlayerChunk = new(-999, -999);

	private int terrainSeed = 0;
	private Vector2I worldOffset; // prevents samping noise at (0,0)

	private Dictionary<string, TileType> tileTypes = new();
	private RandomNumberGenerator rng;


	private class TileType
	{
		public int Id;
		public string Name;
	}

	public override void _Ready()
	{
		var worldUtils = GetNode<GodotObject>("/root/WorldUtils");
		worldUtils.Set("world", this);

		rng = new RandomNumberGenerator();
		rng.Randomize();

		worldOffset = new Vector2I(
			(int)rng.Randi() % 100000,
			(int)rng.Randi() % 100000
		);

		InitTileTypes();
		SetupNoise();
		ruleRegistry = new RuleRegistry(terrainSeed, worldOffset);
		StartWorkerThread();
	}


	public override void _ExitTree()
	{
		running = false;
		workerThread?.Join();
	}

	public override void _Process(double delta)
	{
		while (finaliseQueue.TryDequeue(out var data))
			FinaliseChunk(data);
		
		UpdatePlayerChunks();
	}


	private void SetupNoise()
	{
		terrainSeed = (int)rng.Randi();

		TempNoise = new FastNoiseLite()
		{
			Seed = terrainSeed + 1000,
			NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
			Frequency = 0.0015f,
			FractalOctaves = 3,
			FractalGain = 0.5f,
			FractalLacunarity = 2.0f
		};

		HumidityNoise = new FastNoiseLite()
		{
			Seed = terrainSeed + 2000,
			NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
			Frequency = 0.003f,
			FractalOctaves = 4,
			FractalGain = 0.55f,
			FractalLacunarity = 2.0f
		};

		RiverNoise = new FastNoiseLite()
        {
            Seed = terrainSeed + 3000,
			NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
			Frequency = 0.0025f,
			FractalOctaves = 3,
			FractalGain = 0.5f,
			FractalLacunarity = 2f
        };
	}


	private void StartWorkerThread()
	{
		running = true;
		workerThread = new Thread(WorkerLoop);
		workerThread.Start();
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

	private void UpdatePlayerChunks()
	{
		int C = ChunkSize;

		Vector3 pos = Player.GlobalPosition;
		Vector2I playerTile = new Vector2I(
			Mathf.FloorToInt(pos.X),
			Mathf.FloorToInt(pos.Z)
		);

		Vector2I playerChunk = new Vector2I(
			Mathf.FloorToInt((float)playerTile.X / C),
			Mathf.FloorToInt((float)playerTile.Y / C)
		);

		if (playerChunk == lastPlayerChunk)
			return;

		lastPlayerChunk = playerChunk;

		RequestChunksAround(playerChunk);
		UnloadChunksOutside(playerChunk);
	}

	private void RequestChunksAround(Vector2I center)
    {
        int r = ChunkRadius;

		for (int x = -r; x <= r; x++)
        {
            for (int y = -r; y <= r; y++)
			{
				Vector2I coord = new Vector2I(center.X + x, center.Y + y);

				if (!ActiveChunks.ContainsKey(coord))
					buildQueue.Enqueue(coord);
			}
        }
    }

	private void UnloadChunksOutside(Vector2I center)
    {
        int r = ChunkRadius;
		var toRemove = new List<Vector2I>();

		foreach(var kv in ActiveChunks)
        {
            Vector2I c = kv.Key;
			if(Math.Abs(c.X - center.X) > r || Math.Abs(c.Y - center.Y) > r)
				toRemove.Add(c);
        }

		foreach (var coord in toRemove)
        {
            RemoveChunk(coord);
        }
    }

	private void RemoveChunk(Vector2I coord)
    {
        if (!ActiveChunks.TryGetValue(coord, out Chunk chunk))
			return;
		
		int C = ChunkSize;
		int baseX = coord.X * C;
		int baseY = coord.Y * C;

		Vector3I pos = new Vector3I();
		for (int x = 0; x < C; x++)
        {
            for (int y = 0; y < C; y++)
            {
                pos.X = baseX + x;
				pos.Y = 0;
				pos.Z = baseY + y;

				var tile = chunk.Tiles[x, y];

				if(tile.Type == "water")
					WaterMap.SetCellItem(pos, -1);
				else
					GroundMap.SetCellItem(pos, -1);	
            }
        }

		foreach(var obj in chunk.Objects)
        {
			if (obj == null || !IsInstanceValid(obj))
				continue;

            obj.QueueFree();
        }
		
		foreach (var decor in chunk.Decors)
        {
            if (decor == null || !IsInstanceValid(decor))
				continue;

			decor.QueueFree();
        }

		ActiveChunks.Remove(coord);
    }

	public void RemoveChunkObject(Node3D obj)
    {
        if (obj is WorldObject wo && wo.Chunk != null)
        {
            wo.Chunk.Objects.Remove(obj);
        }
		else
        {
            foreach (var chunk in ActiveChunks.Values)
            {
                if (chunk.Objects.Contains(obj))
                {
                    chunk.Objects.Remove(obj);
					break;
                }
            }
        }
    }

	private ChunkData BuildChunkData(Vector2I coord)
	{
		var sw = System.Diagnostics.Stopwatch.StartNew();

		int C = ChunkSize;
		Tile[,] tiles = new Tile[C, C];
		var objects = new Godot.Collections.Array<ChunkObject>();
		var decors = new Godot.Collections.Array<ChunkDecor>();

		for (int x = 0; x < C; x++)
		{
			for (int y = 0; y < C; y++)
			{
				int globalX = coord.X * C + x;
				int globalY = coord.Y * C + y;

				float tempRaw = TempNoise.GetNoise2D(globalX + worldOffset.X, globalY + worldOffset.Y);
				float humidityRaw = HumidityNoise.GetNoise2D(globalX + worldOffset.X, globalY + worldOffset.Y);
				float riverVal = RiverNoise.GetNoise2D(globalX, globalY + worldOffset.Y);

				float temp = AdjustContrast((tempRaw + 1f) / 2f);
				float humidity = AdjustContrast((humidityRaw + 1f) / 2f);
				float riverDist = Math.Abs(riverVal);


				// determine biome
				BiomePlacementRule biome = ruleRegistry.GetBiome(temp, humidity);
				string tileType = biome.GroundTileType;
				int tileId = GetTileId(tileType);

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
					foreach (BiomeObjectSpawnRule spawn in biome.ObjectSpawnRules)
					{
						if (spawn.Rule.ShouldPlace(globalX, globalY, spawn.Density))
						{
							var obj = new ChunkObject();
							obj.BiomeRule = spawn;
							obj.Position = new Vector3(globalX + 0.25f, 0, globalY + 0.25f);
							objects.Add(obj);
						}
					}

					foreach (DecorPlacementRule decorRule in biome.DecorPlacementRules)
                    {
                        if(decorRule.ShouldPlace(globalX, globalY))
                        {
                            var dec = new ChunkDecor();
							dec.Rule = decorRule;
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
		if (ActiveChunks.ContainsKey(coord))
			return;
		
		int C = ChunkSize;

		Chunk chunk = new Chunk(C)
		{
			Coord = coord,
			Tiles = data.Tiles
		};

		ActiveChunks[coord] = chunk;

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
                    WaterMap.SetCellItem(pos, id);
                }
				else
                {
					GroundMap.SetCellItem(pos, id);
                }
			}
		}

		foreach (ChunkObject obj in data.Objects)
		{
			var rule = obj.BiomeRule.Rule;
			var AllowedVariants = obj.BiomeRule.AllowedVariants;

			var variant = PickObjectVariant(rule, AllowedVariants, (int)obj.Position.X, (int)obj.Position.Z);
			var scene = variant.Scene;
			var instance = scene.Instantiate<Node3D>();
			
			if (instance is WorldObject wo)
            {
                wo.World = this;
				wo.Chunk = chunk;
            }

			if (instance.HasMethod("initialise"))
				instance.Call("initialise");

			instance.Position = obj.Position;
			WorldObjects.AddChild(instance);
			chunk.Objects.Add(instance);
		}

		foreach (ChunkDecor decor in data.Decors)
		{
			var scene = decor.Rule.Scene;
			var instance = scene.Instantiate<Node3D>();
			instance.Position = decor.Position;
			WorldObjects.AddChild(instance);
			chunk.Decors.Add(instance);
		}

		sw.Stop();
		data.FinaliseTimeMs = sw.Elapsed.TotalMilliseconds;

		GD.Print($"Chunk {coord} > Build {data.BuildTimeMs:F3}ms | Finalise {data.FinaliseTimeMs:F3}ms");
	}

	public string GetBiomeAtPos(Vector3 worldPos)
	{
		int C = ChunkSize;

		// Convert world pos → tile pos
		int tileX = Mathf.FloorToInt(worldPos.X);
		int tileY = Mathf.FloorToInt(worldPos.Z);

		// Convert tile → chunk coords
		int cx = Mathf.FloorToInt((float)tileX / C);
		int cy = Mathf.FloorToInt((float)tileY / C);

		Vector2I chunkCoord = new(cx, cy);

		if (!ActiveChunks.TryGetValue(chunkCoord, out Chunk chunk))
			return "";

		// Local tile inside chunk
		int localX = Mathf.Abs(tileX - chunkCoord.X * C);
		int localY = Mathf.Abs(tileY - chunkCoord.Y * C);

		return chunk.Tiles[localX, localY].Biome;
	}

	private void InitTileTypes()
	{
		tileTypes["grass"] = new TileType {
			Id = 0,
			Name = "grass",
		};

		tileTypes["sand"] = new TileType {
			Id = 1,
			Name = "sand",
		};

		tileTypes["snow"] = new TileType {
			Id = 2,
			Name = "snow",
		};
	}


	private int GetTileId(string tileName)
	{
		if (tileTypes.TryGetValue(tileName, out var type))
			return type.Id;
		
		return -1;
	}

	private ObjectVariant PickObjectVariant(ObjectPlacementRule rule, Godot.Collections.Array<ObjectVariant> allowedVariants, int x, int z)
    {
		var valid = allowedVariants.Count > 0 ? allowedVariants : rule.Variants;
		if (valid.Count == 0) return null;

        int hash = (x * 73856093) ^ (z * 19349663) ^ rule.GetHashCode();
		rng.Seed = (ulong)hash;

		float total = 0f;
		foreach (var v in rule.Variants)
			total += v.Weight;

		float r = rng.Randf() * total;

		foreach (var v in rule.Variants)
        {
            if (r <= v.Weight)
                return v;
			
			r -= v.Weight;
        }

		return rule.Variants[0];
    }
}

public partial class ChunkData : RefCounted
{
	public Vector2I Coord;
	public Tile[,] Tiles;
	public Godot.Collections.Array<ChunkObject> Objects;
	public Godot.Collections.Array<ChunkDecor> Decors;

	public double BuildTimeMs;
	public double FinaliseTimeMs;

	public ChunkData(Vector2I coord, Tile[,] tiles, Godot.Collections.Array<ChunkObject> objects, Godot.Collections.Array<ChunkDecor> decors)
	{
		Coord = coord;
		Tiles = tiles;
		Objects = objects;
		Decors = decors;
	}
}