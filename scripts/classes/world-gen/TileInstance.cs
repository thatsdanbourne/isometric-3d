public struct TileInstance(TileDefinition definition, string biome, float temp, float humidity)
{
    public readonly TileDefinition Definition = definition;
    public readonly string Biome = biome;
    public float Temp = temp;
    public float Humidity = humidity;
}