using Godot;
using System.Collections.Generic;

public class StructureSpawnRule
{
	public string Id { get; set; }
	public float Density { get; set; } = 0.05f;
	public int RegionSize = 64;

	public List<StructureVariant> Variants { get; set; } = new();
}

public class StructureVariant
{
	public string Id { get; set; }
	public float Weight { get; set; } = 1f;
}

public class StructureDefinition
{
	public string Id { get; set; }
	public int StableId { get; set; }
	public Vector2I Size { get; set; }
	public List<StructureObject> Objects { get; set; } = new();
}

public class StructureObject
{
	public Vector2I LocalTile { get; set; }
	public float Chance { get; set; } = 1f;

	public List<SpawnVariant> Variants { get; set; } = new();

	public SpawnVariant PickVariant(int seed, int structureId, Vector2I originTile)
	{
		var totalWeight = 0f;

		foreach (var v in Variants)
			totalWeight += v.Weight;

		var hash = DeterministicHash.Combine32(seed, structureId, originTile.X, originTile.Y, LocalTile.X, LocalTile.Y);

		var roll = hash / (float)uint.MaxValue * totalWeight;

		foreach (var v in Variants)
		{
			if (roll <= v.Weight)
				return v;

			roll -= v.Weight;
		}

		return Variants[0];
	}
}