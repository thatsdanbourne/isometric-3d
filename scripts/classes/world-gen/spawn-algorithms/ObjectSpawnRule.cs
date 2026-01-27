using Godot;
using System;
using System.Collections.Generic;

public class ObjectSpawnRule
{
	public string Id { get; set; }
	public SpawnAlgorithm Algorithm { get; set; }
	public float Density { get; set; } = 0.5f;
	public List<SpawnVariant> Variants { get; set; } = new();

	public bool ShouldPlace(int x, int y)
	{
		return Algorithm.ShouldPlace(x, y, Density);
	}

	public SpawnVariant PickVariant(int seed, int x, int y)
	{
		float totalWeight = 0;
		foreach (var v in Variants)
			totalWeight += v.Weight;

		int hash = HashCode.Combine(seed, Id, x, y);
		uint u = (uint)hash;
		float t = u / (float)uint.MaxValue;

		float roll = t * totalWeight;

		foreach (var v in Variants)
		{
			if (roll <= v.Weight)
				return v;

			roll -= v.Weight;
		}

		return Variants[0];
	}
}
