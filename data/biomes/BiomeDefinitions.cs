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
					Id = "plains_trees",
					Density = 0.35f,
					Algorithm = NoisePresets.PlainsTrees(seed, worldOffset),
					Variants =
					{
						new SpawnVariant { Id = "tree_pine", Weight = 1f },
						new SpawnVariant { Id = "tree_birch", Weight = 1f },
					}
				},
				new ObjectSpawnRule
				{
					Id = "plains_rocks",
					Density = 0.3f,
					Algorithm = NoisePresets.OreClusters(seed, worldOffset),
					Variants =
					{
						new SpawnVariant { Id = "rock", Weight = 0.75f },
						new SpawnVariant { Id = "rock_coal", Weight = 0.25f },
					}
				}
			},
			DecorRules =
			{
				new DecorSpawnRule
				{
					DecorId = "flower_poppy",
					Density = 0.3f,
					Algorithm = NoisePresets.Flowers(seed, worldOffset)
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
					Id = "forest_trees",
					Density = 0.65f,
					Algorithm = NoisePresets.ForestTrees(seed, worldOffset),
					Variants =
					{
						new SpawnVariant { Id = "tree_pine", Weight = 1f },
						new SpawnVariant { Id = "tree_birch", Weight = 1f },
					}
				},
				new ObjectSpawnRule
				{
					Id = "forest_rocks",
					Density = 0.25f,
					Algorithm = NoisePresets.OreClusters(seed, worldOffset),
					Variants =
					{
						new SpawnVariant { Id = "rock", Weight = 1f },
						new SpawnVariant { Id = "rock_coal", Weight = 0.15f },
					}
				}
			},
			DecorRules =
			{
				new DecorSpawnRule
				{
					DecorId = "flower_poppy",
					Density = 0.3f,
					Algorithm = NoisePresets.Flowers(seed, worldOffset)
				}
			}
		});

		RuleRegistry.RegisterBiome(new BiomeDefinition
		{
			Name = "Desert",
			GroundTileType = "sand",
			MinTemp = 0.7f,
			MaxTemp = 1.0f,
			MinHumidity = 0.0f,
			MaxHumidity = 0.35f,
			ObjectRules =
			{
				new ObjectSpawnRule
				{
					Id = "desert_rocks",
					Density = 0.25f,
					Algorithm = NoisePresets.OreClusters(seed, worldOffset),
					Variants =
					{
						new SpawnVariant { Id = "rock", Weight = 0.8f },
						new SpawnVariant { Id = "rock_coal", Weight = 0.2f },
					},
				}
			},
		});

		RuleRegistry.RegisterBiome(new BiomeDefinition
		{
			Name = "Tundra",
			GroundTileType = "snow",
			MinTemp = 0.0f,
			MaxTemp = 0.25f,
			MinHumidity = 0.2f,
			MaxHumidity = 0.6f,

			ObjectRules =
			{
				new ObjectSpawnRule
				{
					Id = "tundra_trees",
					Density = 0.3f,
					Algorithm = NoisePresets.TundraTrees(seed, worldOffset),
					Variants =
					{
						new SpawnVariant { Id = "tree_pine", Weight = 1f },
					}
				},
				new ObjectSpawnRule
				{
					Id = "tundra_rocks",
					Density = 0.25f,
					Algorithm = NoisePresets.OreClusters(seed, worldOffset),
					Variants =
					{
						new SpawnVariant { Id = "rock", Weight = 1f },
						new SpawnVariant { Id = "rock_coal", Weight = 0.15f },
					},
				},
			}
		});

		RuleRegistry.RegisterBiome(new BiomeDefinition
		{
			Name = "Taiga",
			GroundTileType = "snow",
			MinTemp = 0.15f,
			MaxTemp = 0.45f,
			MinHumidity = 0.4f,
			MaxHumidity = 1f,

			ObjectRules =
			{
				new ObjectSpawnRule
				{
					Id = "taiga_trees",
					Density = 0.55f,
					Algorithm = NoisePresets.TaigaTrees(seed, worldOffset),
					Variants =
					{
						new SpawnVariant { Id = "tree_pine", Weight = 2f },
						new SpawnVariant { Id = "tree_birch", Weight = 0.5f }
					}
				},
				new ObjectSpawnRule
				{
					Id = "taiga_rocks",
					Density = 0.25f,
					Algorithm = NoisePresets.OreClusters(seed, worldOffset),
					Variants =
					{
						new SpawnVariant { Id = "rock", Weight = 1f },
						new SpawnVariant { Id = "rock_coal", Weight = 0.15f },
					},
				},
			},
		});
	}
}
