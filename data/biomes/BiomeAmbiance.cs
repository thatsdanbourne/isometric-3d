using System.Collections.Generic;

public class BiomeAmbiance(string dayKey, string nightKey)
{
	public readonly string DayAmbianceKey = dayKey;
	public readonly string NightAmbianceKey = nightKey;
}

public static class BiomeAmbiances
{
	public static readonly Dictionary<BiomeId, BiomeAmbiance> AmbianceMap = new()
	{
		{ BiomeId.Plains, new BiomeAmbiance("forest_day", "forest_night") },
		{ BiomeId.Forest, new BiomeAmbiance("forest_day", "forest_night") },
		{ BiomeId.Desert, new BiomeAmbiance("desert", "desert") },
		{ BiomeId.Tundra, new BiomeAmbiance("snowstorm", "snowstorm") },
		{ BiomeId.Taiga, new BiomeAmbiance("snowstorm", "snowstorm") }
	};
}