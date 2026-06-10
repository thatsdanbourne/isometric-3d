using System;
using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class DetailMeshManager : Node
{
	private const int DetailChunkRadius = 2;
	private readonly HashSet<Vector2I> _visibleDetailChunks = new();

	private readonly Dictionary<Vector2I, List<MultiMeshInstance3D>> _detailsByChunk = new();

	private World _world;

	private ShaderMaterial _grassMaterial = GD.Load<ShaderMaterial>("res://resources/materials/grass_01.tres");

	public override void _Ready()
	{
		_world = GetParent<World>();
	}

	public void BuildForChunk(Chunk chunk)
	{
		if (_detailsByChunk.ContainsKey(chunk.Coord))
			return;

		var byMesh = new Dictionary<string, List<Transform3D>>();

		var c = _world.ChunkSize;
		var baseX = chunk.Coord.X * c;
		var baseY = chunk.Coord.Y * c;

		for (var x = 0; x < c; x++)
		for (var y = 0; y < c; y++)
		{
			var tile = chunk.Tiles[x, y];
			var tileCoord = new Vector2I(baseX + x, baseY + y);

			if (_world.IsTileBlocked(tileCoord)) continue;

			foreach (var rule in tile.Definition.DetailMeshes)
				AddInstancesForTile(
					chunk.Coord,
					tileCoord.X,
					tileCoord.Y,
					rule,
					byMesh
				);
		}

		var nodes = new List<MultiMeshInstance3D>();

		foreach (var kv in byMesh)
		{
			var mesh = DetailMeshRegistry.Get(kv.Key);
			if (mesh == null)
				continue;

			var transforms = kv.Value;
			var multiMesh = new MultiMesh
			{
				Mesh = mesh,
				TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
				InstanceCount = transforms.Count
			};

			for (var i = 0; i < transforms.Count; i++)
				multiMesh.SetInstanceTransform(i, transforms[i]);

			var instance = new MultiMeshInstance3D
			{
				Name = $"Detail_{kv.Key}_{chunk.Coord.X}_{chunk.Coord.Y}",
				Multimesh = multiMesh,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
				MaterialOverride = _grassMaterial
			};

			AddChild(instance);
			nodes.Add(instance);
		}

		_detailsByChunk[chunk.Coord] = nodes;
	}

	private void AddInstancesForTile(
		Vector2I chunkCoord,
		int globalX,
		int globalY,
		DetailMeshRule rule,
		Dictionary<string, List<Transform3D>> byMesh)
	{
		var hash = DeterministicHash.CombineU32(
			_world.TerrainSeed,
			chunkCoord.X,
			chunkCoord.Y,
			globalX,
			globalY,
			DeterministicHash.String32(rule.MeshId)
		);

		var rng = new Random((int)hash);

		// Tile-level density gate
		if (rng.NextSingle() > rule.Density)
			return;

		var count = rng.Next(rule.MinPerTile, rule.MaxPerTile);

		if (!byMesh.TryGetValue(rule.MeshId, out var transforms))
		{
			transforms = new List<Transform3D>();
			byMesh[rule.MeshId] = transforms;
		}

		for (var i = 0; i < count; i++)
		{
			var offsetX = (float)rng.NextDouble() - 0.5f;
			var offsetZ = (float)rng.NextDouble() - 0.5f;

			var position = new Vector3(
				globalX + offsetX,
				0f,
				globalY + offsetZ
			);

			var scale = Mathf.Lerp(
				rule.MinScale,
				rule.MaxScale,
				(float)rng.NextDouble()
			);

			var basis = Basis.Identity
				.Scaled(new Vector3(scale, scale, scale));

			transforms.Add(new Transform3D(
				basis,
				position
			));
		}
	}

	public void UpdateVisibleDetails(
		Vector3 playerPosition,
		Dictionary<Vector2I, Chunk> activeChunks)
	{
		var playerChunk = TileUtils.WorldToChunk(playerPosition);
		var desired = new HashSet<Vector2I>();

		for (var x = -DetailChunkRadius; x <= DetailChunkRadius; x++)
		for (var y = -DetailChunkRadius; y <= DetailChunkRadius; y++)
			desired.Add(playerChunk + new Vector2I(x, y));

		// Remove far details
		foreach (var coord in _visibleDetailChunks.ToArray())
		{
			if (desired.Contains(coord))
				continue;

			RemoveChunk(coord);
			_visibleDetailChunks.Remove(coord);
		}

		// Build near details
		foreach (var coord in desired)
		{
			if (_visibleDetailChunks.Contains(coord))
				continue;

			if (!activeChunks.TryGetValue(coord, out var chunk))
				continue;

			BuildForChunk(chunk);
			_visibleDetailChunks.Add(coord);
		}
	}

	public void RemoveChunk(Vector2I chunkCoord)
	{
		if (!_detailsByChunk.TryGetValue(chunkCoord, out var nodes))
			return;

		foreach (var node in nodes)
			if (IsInstanceValid(node))
				node.QueueFree();

		_detailsByChunk.Remove(chunkCoord);
	}
}