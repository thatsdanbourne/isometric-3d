using Godot;

public static class BiomeDefinitions
{
	public static void RegisterAll(int seed, Vector2I worldOffset)
	{
		RuleRegistry.RegisterBiome(new BiomeDefinition
		{
			Name = "Plains",
			GroundTileType = "grass",
			MinTemp = 0.3f,
			MaxTemp = 0.7f,
			MinHumidity = 0.3f,
			MaxHumidity = 0.6f,
			ObjectRules =
			{
				new ObjectSpawnRule
				{
					Density = 0.32f,
					Algorithm = new NoiseAlgorithm(seed, worldOffset),
					Variants =
					{
						new SpawnVariant { Id = "tree_pine", Weight = 1f },
						new SpawnVariant { Id = "tree_birch", Weight = 1f },
					}
				}
			},
			DecorRules =
			{
				new DecorSpawnRule
				{
					DecorId = "poppy",
					Density = 0.3f,
					Algorithm = new NoiseAlgorithm(seed, worldOffset)
				}
			}
		});

		RuleRegistry.RegisterBiome(new BiomeDefinition
		{
			Name = "Forest",
			GroundTileType = "grass",
			MinTemp = 0.3f,
			MaxTemp = 0.7f,
			MinHumidity = 0.6f,
			MaxHumidity = 1.0f,
			ObjectRules =
			{
				new ObjectSpawnRule
				{
					Density = 0.6f,
					Algorithm = new NoiseAlgorithm(seed, worldOffset),
					Variants =
					{
						new SpawnVariant { Id = "tree_pine", Weight = 1f },
						new SpawnVariant { Id = "tree_birch", Weight = 1f },
					}
				}
			},
			DecorRules =
			{
				new DecorSpawnRule
				{
					DecorId = "poppy",
					Density = 0.3f,
					Algorithm = new NoiseAlgorithm(seed, worldOffset)
				}
			}
		});

		RuleRegistry.RegisterBiome(new BiomeDefinition
		{
			Name = "Tundra",
			GroundTileType = "snow",
			MinTemp = 0.0f,
			MaxTemp = 0.3f,
			MinHumidity = 0.0f,
			MaxHumidity = 0.6f,
			ObjectRules =
			{
				new ObjectSpawnRule
				{
					Density = 0.32f,
					Algorithm = new NoiseAlgorithm(seed, worldOffset),
					Variants =
					{
						new SpawnVariant { Id = "tree_pine", Weight = 1f },
						new SpawnVariant { Id = "tree_birch", Weight = 1f },
					}
				}
			},
			DecorRules =
			{
				new DecorSpawnRule
				{
					DecorId = "poppy",
					Density = 0.3f,
					Algorithm = new NoiseAlgorithm(seed, worldOffset)
				}
			}
		});
	}
}
