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

	public readonly Dictionary<Vector2I, Chunk> ActiveChunks
		= new Dictionary<Vector2I, Chunk>();


	public Vector2I lastPlayerChunk = new(-999, -999);

	private int terrainSeed = 0;
	public Vector2I worldOffset; // prevents sampling noise at (0,0)

	private Dictionary<string, TileType> tileTypes = new();
	private RandomNumberGenerator rng;

	public ChunkManager ChunkManager { get; private set; }
	public ChunkGenerator ChunkGenerator { get; private set; }

	private class TileType
	{
		public int Id;
		public string Name;
	}


	public override void _Ready()
	{
		rng = new RandomNumberGenerator();
		rng.Randomize();

		worldOffset = new Vector2I(
			(int)rng.Randi() % 100000,
			(int)rng.Randi() % 100000
		);

		InitTileTypes();
		SetupNoise();

		RuleRegistry.LoadAll(terrainSeed, worldOffset);

		ChunkManager = new ChunkManager(this, ChunkSize, ChunkRadius);

		ChunkGenerator = new ChunkGenerator(this, ChunkManager);
		ChunkGenerator.Start();
	}

	public override void _ExitTree()
	{
		ChunkGenerator?.Stop();
	}

	public override void _PhysicsProcess(double delta)
	{
		ChunkManager.UpdateChunks(Player.GlobalPosition);
		ChunkGenerator.Update();
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

	public string GetBiomeAtPos(Vector3 worldPos)
	{
		int tileX = (int)worldPos.X;
		int tileY = (int)worldPos.Z;

		int shift = (int)Math.Log2(ChunkSize);

		int cx = tileX >> shift;
		int cy = tileY >> shift;

		Vector2I chunkCoord = new(cx, cy);

		if (!ActiveChunks.TryGetValue(chunkCoord, out Chunk chunk))
			return "";

		// Local tile inside chunk
		int localX = tileX & (ChunkSize - 1);
		int localY = tileY & (ChunkSize - 1);

		return chunk.Tiles[localX, localY].Biome;
	}

	private void InitTileTypes()
	{
		tileTypes["grass"] = new TileType
		{
			Id = 0,
			Name = "grass",
		};

		tileTypes["sand"] = new TileType
		{
			Id = 1,
			Name = "sand",
		};

		tileTypes["snow"] = new TileType
		{
			Id = 2,
			Name = "snow",
		};
	}


	public int GetTileId(string tileName)
	{
		if (tileTypes.TryGetValue(tileName, out var type))
			return type.Id;

		return -1;
	}

	public SpawnVariant PickObjectVariant(ObjectSpawnRule rule, List<SpawnVariant> allowedVariants, int x, int z)
	{
		var valid = allowedVariants.Count > 0 ? allowedVariants : rule.Variants;
		if (valid.Count == 0) return null;

		int hash = (x * 73856093) ^ (z * 19349663) ^ rule.GetHashCode();
		rng.Seed = (ulong)hash;

		float total = 0f;
		foreach (var v in valid)
			total += v.Weight;

		float r = rng.Randf() * total;

		foreach (var v in valid)
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