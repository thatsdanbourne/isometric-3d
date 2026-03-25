using Godot;

public static class BiomeDefinitions
{
	public static void RegisterAll(int seed, Vector2I worldOffset)
	{
		RuleRegistry.RegisterBiome(new BiomeDefinition
		{
			Id = BiomeId.Plains,
			Name = "Plains",
			GroundTileId = TileId.Grass,
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
						new SpawnVariant { Id = "tree_oak", Weight = 1f },
						new SpawnVariant { Id = "tree_birch", Weight = 1f }
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
						new SpawnVariant { Id = "rock_coal", Weight = 0.2f },
						new SpawnVariant { Id = "rock_copper", Weight = 0.05f }
					}
				},
				new ObjectSpawnRule
				{
					Id = "plains_flowers",
					Density = 0.3f,
					Algorithm = NoisePresets.Flowers(seed, worldOffset),
					Variants =
					{
						new SpawnVariant { Id = "flower_poppy", Weight = 0.5f }
					}
				}
			},
			MobRules =
			{
				new MobSpawnRule
				{
					Id = "plains_deer",
					MobId = "deer",
					ChunkChance = 0.2f,
					MinPerChunk = 0,
					MaxPerChunk = 2
				},
				new MobSpawnRule
				{
					Id = "plains_bandit",
					MobId = "bandit",
					ChunkChance = 0.05f,
					MinPerChunk = 0,
					MaxPerChunk = 3
				}
			}
		});

		RuleRegistry.RegisterBiome(new BiomeDefinition
		{
			Id = BiomeId.Forest,
			Name = "Forest",
			GroundTileId = TileId.Grass,
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
						new SpawnVariant { Id = "tree_oak", Weight = 1f },
						new SpawnVariant { Id = "tree_birch", Weight = 1f }
					}
				},
				new ObjectSpawnRule
				{
					Id = "forest_rocks",
					Density = 0.25f,
					Algorithm = NoisePresets.OreClusters(seed, worldOffset),
					Variants =
					{
						new SpawnVariant { Id = "rock", Weight = 0.8f },
						new SpawnVariant { Id = "rock_coal", Weight = 0.15f },
						new SpawnVariant { Id = "rock_copper", Weight = 0.05f }
					}
				},
				new ObjectSpawnRule
				{
					Id = "plains_flowers",
					Density = 0.3f,
					Algorithm = NoisePresets.Flowers(seed, worldOffset),
					Variants =
					{
						new SpawnVariant { Id = "flower_poppy", Weight = 0.5f }
					}
				}
			}
		});

		RuleRegistry.RegisterBiome(new BiomeDefinition
		{
			Id = BiomeId.Desert,
			Name = "Desert",
			GroundTileId = TileId.Sand,
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
						new SpawnVariant { Id = "rock_coal", Weight = 0.2f }
					}
				}
			}
		});

		RuleRegistry.RegisterBiome(new BiomeDefinition
		{
			Id = BiomeId.Tundra,
			Name = "Tundra",
			GroundTileId = TileId.Snow,
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
						new SpawnVariant { Id = "tree_pine", Weight = 1f }
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
						new SpawnVariant { Id = "rock_copper", Weight = 0.02f }
					}
				}
			}
		});

		RuleRegistry.RegisterBiome(new BiomeDefinition
		{
			Id = BiomeId.Taiga,
			Name = "Taiga",
			GroundTileId = TileId.Snow,
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
						new SpawnVariant { Id = "rock_copper", Weight = 0.025f }
					}
				}
			}
		});

		RuleRegistry.RegisterBiome(new BiomeDefinition
		{
			Id = BiomeId.River,
			Name = "River",
			Kind = BiomeKind.Overlay,
			GroundTileId = TileId.Water
		});

		RuleRegistry.RegisterBiome(new BiomeDefinition
		{
			Id = BiomeId.Riverbank,
			Name = "Riverbank",
			Kind = BiomeKind.Overlay,
			GroundTileId = TileId.Sand,
			ObjectRules =
			{
				new ObjectSpawnRule
				{
					Id = "riverbank_reeds",
					Density = 0.3f,
					Algorithm = NoisePresets.Reeds(seed, worldOffset),
					Variants =
					{
						new SpawnVariant { Id = "reeds", Weight = 1f }
					},
					Conditions = new SpawnConditions
					{
						NeighbourRequirements =
						{
							new NeighbourRequirement
							{
								TargetType = NeighbourTargetType.WaterFeature,
								TargetId = nameof(WaterFeatureType.River),
								Radius = 3,
								MinCount = 1
							}
						},
						DensityModifiers =
						{
							new DensityModifier
							{
								TargetType = NeighbourTargetType.WaterFeature,
								TargetId = nameof(WaterFeatureType.River),
								Radius = 3,
								MinCount = 1,
								MaxCount = 4,
								MinMultiplier = 0.4f,
								MaxMultiplier = 1.5f
							}
						}
					}
				}
			}
		});

		RuleRegistry.RegisterBiome(new BiomeDefinition
		{
			Id = BiomeId.Lake,
			Name = "Lake",
			Kind = BiomeKind.Overlay,
			GroundTileId = TileId.Water
		});

		RuleRegistry.RegisterBiome(new BiomeDefinition
			{
				Id = BiomeId.LakeShore,
				Name = "Lake Shore",
				Kind = BiomeKind.Overlay,
				GroundTileId = TileId.Sand
			}
		);
	}
}