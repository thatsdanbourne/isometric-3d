using System.Collections.Generic;
using Godot;

public static class RuleRegistry
{
	private static readonly List<BiomeDefinition> BiomeList = [];

	public static void RegisterBiome(BiomeDefinition biome)
	{
		BiomeList.Add(biome);
	}

	public static void LoadAll(int seed, Vector2I worldOffset)
	{
		BiomeList.Clear();
		BiomeDefinitions.RegisterAll(seed, worldOffset);
	}

	public static BiomeDefinition GetBiome(float temp, float humidity)
	{
		foreach (var biome in BiomeList)
			if (biome.Matches(temp, humidity))
				return biome;

		return BiomeList[0];
	}

	public static BiomeDefinition GetBiomeById(BiomeId biomeId)
	{
		foreach (var biome in BiomeList)
			if (biome.Id == biomeId)
				return biome;

		return BiomeList[0];
	}
}