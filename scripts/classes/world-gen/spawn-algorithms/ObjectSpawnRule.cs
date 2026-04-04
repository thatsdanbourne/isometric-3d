using System;
using System.Collections.Generic;

public class ObjectSpawnRule
{
	public string Id { get; set; }
	public int StableId { get; set; }

	public SpawnAlgorithm Algorithm { get; set; }
	public float Density { get; set; } = 0.5f;
	public List<SpawnVariant> Variants { get; set; } = new();
	public SpawnConditions Conditions { get; set; } = new();

	public ObjectSpawnRule(string id)
	{
		Id = id;
		StableId = DeterministicHash.String32(id);
	}

	public bool ShouldPlace(int x, int y)
	{
		return Algorithm.ShouldPlace(x, y, Density);
	}

	public SpawnVariant PickVariant(int seed, int x, int y)
	{
		if (Variants.Count == 1)
			return Variants[0];

		float totalWeight = 0;
		foreach (var v in Variants)
			totalWeight += v.Weight;

		var hash = DeterministicHash.CombineU32(seed, StableId, x, y);
		var t = hash / (float)uint.MaxValue;
		var roll = t * totalWeight;

		foreach (var v in Variants)
		{
			if (roll < v.Weight)
				return v;

			roll -= v.Weight;
		}

		return Variants[0];
	}
}