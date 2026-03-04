using System.Collections.Generic;

public class BiomeDefinition
{
	public BiomeId Id { get; set; }
	public string Name { get; set; }
	public TileId GroundTileId { get; set; }

	public float MinTemp { get; set; }
	public float MaxTemp { get; set; }
	public float MinHumidity { get; set; }
	public float MaxHumidity { get; set; }

	public List<ObjectSpawnRule> ObjectRules { get; } = new();
	public List<MobSpawnRule> MobRules { get; } = new();

	public bool Matches(float temp, float humidity)
	{
		return temp >= MinTemp && temp <= MaxTemp &&
		       humidity >= MinHumidity && humidity <= MaxHumidity;
	}
}