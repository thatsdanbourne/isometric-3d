using Godot;

public struct Tile
{
    public int Id;
    public string Type;
    public string Biome;
    public float Temp;
    public float Humidity;

    public Tile(int id, string type, string biome, float temp, float humidity)
    {
        this.Id = id;
        this.Type = type;
        this.Biome = biome;
        this.Temp = temp;
        this.Humidity = humidity;
    }
}