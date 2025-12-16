using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

public partial class World : Node3D
{
	[Signal] public delegate void WorldReadyEventHandler();
	[Export] public Node3D WorldObjects;
	[Export] public GridMap GroundMap;
	[Export] public GridMap WaterMap;
	[Export] public Node3D PlayerContainer;

	public Player Player;

	public int ChunkSize = 16;
	public int ChunkRadius = 3;

	public FastNoiseLite TempNoise;
	public FastNoiseLite HumidityNoise;
	public FastNoiseLite RiverNoise;

	public readonly Dictionary<Vector2I, Chunk> ActiveChunks
		= new Dictionary<Vector2I, Chunk>();

	private readonly Dictionary<Vector2I, int> blockedTiles = new();


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

		ChunkGenerator = new ChunkGenerator(this, ChunkManager, terrainSeed);
		ChunkGenerator.Start();

		GameManager.Instance.RegisterWorld(this);
		EmitSignal(SignalName.WorldReady);

		GameManager.Instance.LocalPlayerChanged += (p) =>
		{
			Player = p;
			p.PlayerReady += () =>
			{
				ChunkManager.ForceInitialChunks(p.GlobalPosition);
				ChunkGenerator.InitialChunksReady += () => p.CheckBiome();
			};
		};

		GameManager.Instance.SpawnLocalPlayer();

	}

	public override void _ExitTree()
	{
		ChunkGenerator?.Stop();
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Player == null || !IsInstanceValid(Player) || !Player.IsInsideTree())
			return;

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

	public void BlockTile(Vector2I tile)
	{
		if (blockedTiles.TryGetValue(tile, out int count))
			blockedTiles[tile] = count + 1;
		else
			blockedTiles[tile] = 1;
	}

	public void UnblockTile(Vector2I tile)
	{
		if (!blockedTiles.TryGetValue(tile, out int count)) return;

		count--;

		if (count <= 0)
			blockedTiles.Remove(tile);
		else
			blockedTiles[tile] = count;
	}

	public bool IsTileBlocked(Vector2I tile)
	{
		return blockedTiles.ContainsKey(tile);
	}

	public bool CanPlace(Vector2I tile, PlaceableItem item)
	{
		if (blockedTiles.ContainsKey(tile)) return false;

		return true;
	}

	public void PlaceItem(Vector2I tile, PlaceableItem item)
	{
		Vector3 worldPos = TileManager.TileToWorld(tile);
		Vector2I chunkCoord = TileManager.WorldToChunk(worldPos);

		if (!ActiveChunks.TryGetValue(chunkCoord, out Chunk chunk))
		{
			GD.PrintErr($"Tried to place item in unloaded chunk {chunkCoord}");
			return;
		}

		var obj = item.PlaceableScene.Instantiate<WorldObject>();
		WorldObjects.AddChild(obj);
		obj.GlobalPosition = worldPos;

		chunk.Objects.Add(obj);
		obj.Chunk = chunk;
		BlockTile(tile);
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
			Id = 2,
			Name = "sand",
		};

		tileTypes["snow"] = new TileType
		{
			Id = 3,
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