public struct TileInstance
{
    public TileDefinition Definition;
    public string Biome;
    public float Temp;
    public float Humidity;

    public TileInstance(TileDefinition definition, string biome, float temp, float humidity)
    {
        Definition = definition;
        this.Biome = biome;
        this.Temp = temp;
        this.Humidity = humidity;
    }
}