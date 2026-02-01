using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

public partial class ChunkGenerator : Node
{
    [Signal]
    public delegate void InitialChunksReadyEventHandler();

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
        { "snow", new[] { 4 } }
    };

    private Dictionary<string, float[]> tileVariantWeights = new()
    {
        { "grass", new[] { 0.795f, 0.005f } },
        { "sand", new[] { 1f } },
        { "snow", new[] { 1f } }
    };


    public ChunkGenerator(World world, ChunkManager chunkManager, int seed)
    {
        _world = world;
        _chunkManager = chunkManager;
        terrainSeed = seed;
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
        while (finaliseQueue.TryDequeue(out var data)) FinaliseChunk(data);
    }

    public void RequestBuild(Vector2I coord)
    {
        buildQueue.Enqueue(coord);
    }

    private void WorkerLoop()
    {
        while (running)
            if (buildQueue.TryDequeue(out var coord))
            {
                var result = BuildChunk(coord);
                finaliseQueue.Enqueue(result);
            }
            else
            {
                Thread.Sleep(1);
            }
    }

    private Chunk BuildChunk(Vector2I coord)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var C = _world.ChunkSize;
        var tiles = new TileInstance[C, C];
        var objects = new List<ChunkObject>();
        var decors = new List<ChunkDecor>();
        var blocked = new bool[C, C];

        var chunk = new Chunk(coord, tiles, objects, decors, new Dictionary<string, ChunkTileMeshData>());
        ChunkDeltaData chunkDelta;
        _world.TryGetChunkDelta(coord, out chunkDelta);

        for (var x = 0; x < C; x++)
        for (var y = 0; y < C; y++)
        {
            var globalX = coord.X * C + x;
            var globalY = coord.Y * C + y;
            var tilePos = new Vector2I(globalX, globalY);

            var tempRaw = _world.TempNoise.GetNoise2D(globalX + _world.worldOffset.X, globalY + _world.worldOffset.Y);
            var humidityRaw =
                _world.HumidityNoise.GetNoise2D(globalX + _world.worldOffset.X, globalY + _world.worldOffset.Y);
            var riverVal = _world.RiverNoise.GetNoise2D(globalX, globalY + _world.worldOffset.Y);

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
            // foreach (DecorSpawnRule decorRule in biome.DecorRules)
            // {
            // 	if (decorRule.ShouldPlace(globalX, globalY))
            // 	{
            // 		if (blocked[x, y])
            // 			continue;
            // 		var dec = new ChunkDecor();
            // 		dec.DecorRule = decorRule;
            // 		dec.Position = new Vector3(globalX + 0.25f, 0, globalY + 0.25f);
            // 		decors.Add(dec);
            // 	}
            // }
        }

        // build player placed objects
        if (chunkDelta != null)
            foreach (var placed in chunkDelta?.PlacedObjects)
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
        if (_world.ActiveChunks.ContainsKey(coord))
            return;

        var C = _world.ChunkSize;

        _world.ActiveChunks[coord] = chunk;

        if (!_chunkManager.InitialChunksReady &&
            _chunkManager.PendingInitialChunks.Remove(coord) &&
            _chunkManager.PendingInitialChunks.Count == 0)
        {
            _chunkManager.InitialChunksReady = true;
            EmitSignal(SignalName.InitialChunksReady);
        }

        var pos = new Vector3I();
        var baseX = coord.X * C;
        var baseY = coord.Y * C;

        for (var x = 0; x < C; x++)
        for (var y = 0; y < C; y++)
        {
            var id = chunk.Tiles[x, y].Definition.GridTileId;
            pos.X = baseX + x;
            pos.Y = 0;
            pos.Z = baseY + y;

            if (chunk.Tiles[x, y].Definition.Name == "water")
                _world.WaterMap.SetCellItem(pos, id);
            else
                _world.GroundMap.SetCellItem(pos, id);
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
        var seed = (ulong)HashCode.Combine(terrainSeed, x, y);
        rng.Seed = seed;

        var index = rng.RandWeighted(tileVariantWeights[tileType]);
        return tileVariants[tileType][(int)index];
    }
}