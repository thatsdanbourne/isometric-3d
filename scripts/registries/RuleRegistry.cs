using System.Collections.Generic;
using Godot;

public static class RuleRegistry
{
    private static readonly List<BiomeDefinition> _biomes = new();

    public static IReadOnlyList<BiomeDefinition> Biomes => _biomes;


    public static void RegisterBiome(BiomeDefinition biome)
    {
        _biomes.Add(biome);
    }

    public static void LoadAll(int seed, Vector2I worldOffset)
    {
        _biomes.Clear();
        BiomeDefinitions.RegisterAll(seed, worldOffset);
    }

    public static BiomeDefinition GetBiome(float temp, float humidity)
    {
        foreach (var biome in _biomes)
        {
            if (biome.Matches(temp, humidity))
                return biome;
        }

        return _biomes[0];
    }
}