using System.Collections.Generic;

public class BiomeAmbiance
{
    public string DayAmbianceKey;
    public string NightAmbianceKey;

    public BiomeAmbiance(string dayKey, string nightKey)
    {
        DayAmbianceKey = dayKey;
        NightAmbianceKey = nightKey;
    }
}

public static class BiomeAmbiances
{
    public static readonly Dictionary<string, BiomeAmbiance> AmbianceMap = new()
    {
        { "Plains", new BiomeAmbiance("forest_day", "forest_night") },
        { "Forest", new BiomeAmbiance("forest_day", "forest_night") },
        { "Desert", new BiomeAmbiance("desert_wind", "desert_wind") },
        { "Tundra", new BiomeAmbiance("cold_wind", "cold_wind") },
        { "Taiga", new BiomeAmbiance("cold_wind", "cold_wind") },
    };
}
