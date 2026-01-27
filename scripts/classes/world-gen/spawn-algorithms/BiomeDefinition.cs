using System.Collections.Generic;

public class BiomeDefinition
{
	public string Name { get; set; }
	public string GroundTileType { get; set; }

	public float MinTemp { get; set; }
	public float MaxTemp { get; set; }
	public float MinHumidity { get; set; }
	public float MaxHumidity { get; set; }

	public List<ObjectSpawnRule> ObjectRules { get; set; } = new();
	public List<DecorSpawnRule> DecorRules { get; set; } = new();

	public bool Matches(float temp, float humidity)
	{
		return temp >= MinTemp && temp <= MaxTemp &&
			humidity >= MinHumidity && humidity <= MaxHumidity;
	}
}
