using Godot;
using System;
using System.Collections.Generic;

public partial class World : Node3D
{
	[Signal]
	public delegate void WorldReadyEventHandler();

	[Export] public Node3D WorldObjects;
	[Export] public GridMap GroundMap;
	[Export] public GridMap WaterMap;
	[Export] public Node3D PlayerContainer;
	[Export] public Node3D ItemPickupContainer;

	public Player Player;

	public double WorldTimeSeconds;

	public int ChunkSize = TileManager.ChunkSize;
	public int ChunkRadius = 3;

	public FastNoiseLite TempNoise;
	public FastNoiseLite HumidityNoise;
	public FastNoiseLite RiverNoise;

	public readonly Dictionary<Vector2I, Chunk> ActiveChunks = new();
	public Dictionary<Vector2I, ChunkDeltaData> ChunkDeltas = new();
	private readonly Dictionary<Vector2I, int> blockedTiles = new();

	public Vector2I lastPlayerChunk = new(-999, -999);

	private int terrainSeed = 0;
	public Vector2I worldOffset; // prevents sampling noise at (0,0)

	private RandomNumberGenerator rng;

	public ChunkManager ChunkManager { get; private set; }
	public ChunkGenerator ChunkGenerator { get; private set; }
	public WorldObjectManager WorldObjectManager { get; private set; }
	public Node3D WorldObjectPool;


	public override void _Ready()
	{
		rng = new RandomNumberGenerator();
		rng.Randomize();

		worldOffset = new Vector2I(
			(int)rng.Randi() % 100000,
			(int)rng.Randi() % 100000
		);

		SetupNoise();

		RuleRegistry.LoadAll(terrainSeed, worldOffset);

		ChunkManager = new ChunkManager(this, ChunkSize, ChunkRadius);
		WorldObjectManager = GetNode<WorldObjectManager>("WorldObjectManager");
		WorldObjectPool = GetNode<Node3D>("WorldObjectPool");

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

		WorldTimeSeconds += delta;
	}

	private void SetupNoise()
	{
		terrainSeed = (int)rng.Randi();

		TempNoise = new FastNoiseLite
		{
			Seed = terrainSeed + 1000,
			NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
			Frequency = 0.0015f,
			FractalOctaves = 3,
			FractalGain = 0.5f,
			FractalLacunarity = 2.0f
		};

		HumidityNoise = new FastNoiseLite
		{
			Seed = terrainSeed + 2000,
			NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
			Frequency = 0.003f,
			FractalOctaves = 4,
			FractalGain = 0.55f,
			FractalLacunarity = 2.0f
		};

		RiverNoise = new FastNoiseLite
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
		if (blockedTiles.TryGetValue(tile, out var count))
			blockedTiles[tile] = count + 1;
		else
			blockedTiles[tile] = 1;
	}

	public void UnblockTile(Vector2I tile)
	{
		if (!blockedTiles.TryGetValue(tile, out var count)) return;

		count--;

		if (count <= 0)
			blockedTiles.Remove(tile);
		else
			blockedTiles[tile] = count;
	}

	public bool CanPlace(Vector2I tile, PlaceableItem item)
	{
		if (blockedTiles.ContainsKey(tile)) return false;

		return true;
	}

	public bool PlaceItem(Vector2I tile, PlaceableItem item)
	{
		var worldPos = TileManager.TileToWorld(tile);
		var chunkCoord = TileManager.WorldToChunk(worldPos);

		if (!ActiveChunks.ContainsKey(chunkCoord))
		{
			GD.PrintErr($"Tried to place item in unloaded chunk {chunkCoord}");
			return false;
		}

		var def = item.PlaceableObjectDefinition;

		var chunkObj = new ChunkObject
		{
			Definition = def,
			TileCoord = tile,
			Position = worldPos,
			ChunkCoord = chunkCoord,
			Source = ChunkObjectSource.Placed
		};

		return WorldObjectManager.RequestPlace(chunkObj);
	}

	public bool TryGetChunkDelta(Vector2I chunkCoord, out ChunkDeltaData delta)
	{
		return ChunkDeltas.TryGetValue(chunkCoord, out delta);
	}

	public ChunkDeltaData GetOrCreateChunkDelta(Vector2I chunkCoord)
	{
		if (!ChunkDeltas.TryGetValue(chunkCoord, out var delta))
		{
			delta = new ChunkDeltaData();
			ChunkDeltas[chunkCoord] = delta;
		}

		return delta;
	}

	public string GetBiomeAtPos(Vector3 worldPos)
	{
		var tileX = (int)worldPos.X;
		var tileY = (int)worldPos.Z;

		var shift = (int)Math.Log2(ChunkSize);

		var cx = tileX >> shift;
		var cy = tileY >> shift;

		Vector2I chunkCoord = new(cx, cy);

		if (!ActiveChunks.TryGetValue(chunkCoord, out var chunk))
			return "";

		// Local tile inside chunk
		var localX = tileX & (ChunkSize - 1);
		var localY = tileY & (ChunkSize - 1);

		return chunk.Tiles[localX, localY].Biome;
	}
}