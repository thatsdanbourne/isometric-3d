public readonly struct TileInstance(TileDefinition definition, BiomeId biome, float temp, float humidity)
{
	public readonly TileDefinition Definition = definition;
	public readonly BiomeId Biome = biome;
	public readonly float Temp = temp;
	public readonly float Humidity = humidity;
}