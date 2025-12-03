using Godot;
using System.Collections.Generic;

public class ObjectSpawnRule
{
	public SpawnAlgorithm Algorithm { get; set; }
	public float Density { get; set; } = 0.5f;

	public List<SpawnVariant> Variants { get; set; } = new();

	public bool ShouldPlace(int x, int y)
	{
		return Algorithm.ShouldPlace(x, y, Density);
	}

	public SpawnVariant PickVariant(int x, int y)
	{
		float totalWeight = 0;
		foreach (var v in Variants)
			totalWeight += v.Weight;

		float roll = (float)GD.RandRange(0, totalWeight);

		foreach (var v in Variants)
		{
			if (roll <= v.Weight)
				return v;

			roll -= v.Weight;
		}

		return Variants[0];
	}
}
