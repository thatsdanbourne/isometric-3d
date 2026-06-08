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
				new ObjectSpawnRule("plains_trees")
				{
					Density = 0.35f,
					Algorithm = NoisePresets.PlainsTrees(seed, worldOffset),
					Variants =
					{
						new SpawnVariant("tree_oak") { Weight = 1f },
						new SpawnVariant("tree_birch") { Weight = 1f }
					}
				},
				new ObjectSpawnRule("plains_rocks")
				{
					Density = 0.3f,
					Algorithm = NoisePresets.OreClusters(seed, worldOffset),
					Variants =
					{
						new SpawnVariant("rock") { Weight = 0.75f },
						new SpawnVariant("rock_coal") { Weight = 0.2f },
						new SpawnVariant("rock_copper") { Weight = 0.05f }
					}
				},
				new ObjectSpawnRule("plains_plants")
				{
					Pass = ObjectSpawnPass.Decor,
					Density = 0.75f,
					Algorithm = NoisePresets.Flowers(seed, worldOffset),
					Variants =
					{
						new SpawnVariant("tall_grass") { Weight = 3f },
						new SpawnVariant("flower_poppy") { Weight = 1f }
					}
				},
				new ObjectSpawnRule("plains_stones")
				{
					Pass = ObjectSpawnPass.GroundPickups,
					Density = 0.4f,
					Algorithm = NoisePresets.LooseStones(seed, worldOffset),
					Variants = { new SpawnVariant("stone") { Weight = 1f } },
					Conditions = new SpawnConditions
					{
						DensityModifiers =
						{
							new DensityModifier
							{
								TargetType = NeighbourTargetType.Object,
								TargetId = WorldObjectRegistry.GetDefinition("rock").StableId,
								Radius = 3,
								MinCount = 1,
								MaxCount = 3,
								MinMultiplier = 1f,
								MaxMultiplier = 2f
							}
						}
					}
				},
				new ObjectSpawnRule("plains_sticks")
				{
					Pass = ObjectSpawnPass.GroundPickups,
					Density = 0.4f,
					Algorithm = NoisePresets.LooseStones(seed, worldOffset),
					Variants = { new SpawnVariant("stick_pile") { Weight = 1f } },
					Conditions = new SpawnConditions
					{
						DensityModifiers =
						{
							new DensityModifier
							{
								TargetType = NeighbourTargetType.Object,
								TargetId = WorldObjectRegistry.GetDefinition("tree_oak").StableId,
								Radius = 3,
								MinCount = 1,
								MaxCount = 3,
								MinMultiplier = 1f,
								MaxMultiplier = 1.5f
							}
						}
					}
				}
			},
			MobRules =
			{
				new MobSpawnRule("plains_deer")
				{
					MobId = "deer",
					ChunkChance = 0.2f,
					MinPerChunk = 0,
					MaxPerChunk = 2
				},
				new MobSpawnRule("plains_bandit")
				{
					MobId = "bandit",
					ChunkChance = 0.05f,
					MinPerChunk = 0,
					MaxPerChunk = 3
				}
			},
			StructureRules =
			{
				new StructureSpawnRule
				{
					Id = "plains_ruins",
					Density = 1f,
					RegionSize = 64,
					Variants =
					{
						new StructureVariant { Id = "plains_stone_circle", Weight = 1f }
					}
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
				new ObjectSpawnRule("forest_trees")
				{
					Density = 0.65f,
					Algorithm = NoisePresets.ForestTrees(seed, worldOffset),
					Variants =
					{
						new SpawnVariant("tree_oak") { Weight = 1f },
						new SpawnVariant("tree_birch") { Weight = 1f }
					}
				},
				new ObjectSpawnRule("forest_rocks")
				{
					Density = 0.25f,
					Algorithm = NoisePresets.OreClusters(seed, worldOffset),
					Variants =
					{
						new SpawnVariant("rock") { Weight = 0.8f },
						new SpawnVariant("rock_coal") { Weight = 0.15f },
						new SpawnVariant("rock_copper") { Weight = 0.05f }
					}
				},
				new ObjectSpawnRule("forest_flowers")
				{
					Pass = ObjectSpawnPass.Decor,
					Density = 0.7f,
					Algorithm = NoisePresets.Flowers(seed, worldOffset),
					Variants =
					{
						new SpawnVariant("flower_poppy") { Weight = 1f }
					}
				},
				new ObjectSpawnRule("forest_stones")
				{
					Pass = ObjectSpawnPass.GroundPickups,
					Density = 0.4f,
					Algorithm = NoisePresets.LooseStones(seed, worldOffset),
					Variants = { new SpawnVariant("stone") { Weight = 1f } },
					Conditions = new SpawnConditions
					{
						DensityModifiers =
						{
							new DensityModifier
							{
								TargetType = NeighbourTargetType.Object,
								TargetId = WorldObjectRegistry.GetDefinition("rock").StableId,
								Radius = 3,
								MinCount = 1,
								MaxCount = 3,
								MinMultiplier = 1f,
								MaxMultiplier = 2f
							}
						}
					}
				},
				new ObjectSpawnRule("forest_sticks")
				{
					Pass = ObjectSpawnPass.GroundPickups,
					Density = 0.45f,
					Algorithm = NoisePresets.LooseStones(seed, worldOffset),
					Variants = { new SpawnVariant("stick_pile") { Weight = 1f } },
					Conditions = new SpawnConditions
					{
						DensityModifiers =
						{
							new DensityModifier
							{
								TargetType = NeighbourTargetType.Object,
								TargetId = WorldObjectRegistry.GetDefinition("tree_oak").StableId,
								Radius = 3,
								MinCount = 1,
								MaxCount = 3,
								MinMultiplier = 1f,
								MaxMultiplier = 5f
							}
						}
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
				new ObjectSpawnRule("desert_rocks")
				{
					Density = 0.25f,
					Algorithm = NoisePresets.OreClusters(seed, worldOffset),
					Variants =
					{
						new SpawnVariant("rock") { Weight = 0.8f },
						new SpawnVariant("rock_coal") { Weight = 0.2f }
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
				new ObjectSpawnRule("tundra_trees")
				{
					Density = 0.3f,
					Algorithm = NoisePresets.TundraTrees(seed, worldOffset),
					Variants =
					{
						new SpawnVariant("tree_pine") { Weight = 1f }
					}
				},
				new ObjectSpawnRule("tundra_rocks")
				{
					Density = 0.25f,
					Algorithm = NoisePresets.OreClusters(seed, worldOffset),
					Variants =
					{
						new SpawnVariant("rock") { Weight = 1f },
						new SpawnVariant("rock_coal") { Weight = 0.15f },
						new SpawnVariant("rock_copper") { Weight = 0.02f }
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
				new ObjectSpawnRule("taiga_trees")
				{
					Density = 0.55f,
					Algorithm = NoisePresets.TaigaTrees(seed, worldOffset),
					Variants =
					{
						new SpawnVariant("tree_pine") { Weight = 2f },
						new SpawnVariant("tree_birch") { Weight = 0.5f }
					}
				},
				new ObjectSpawnRule("taiga_rocks")
				{
					Density = 0.25f,
					Algorithm = NoisePresets.OreClusters(seed, worldOffset),
					Variants =
					{
						new SpawnVariant("rock") { Weight = 1f },
						new SpawnVariant("rock_coal") { Weight = 0.15f },
						new SpawnVariant("rock_copper") { Weight = 0.025f }
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
				new ObjectSpawnRule("riverbank_reeds")
				{
					Density = 0.3f,
					Algorithm = NoisePresets.Reeds(seed, worldOffset),
					Variants =
					{
						new SpawnVariant("reeds") { Weight = 1f }
					},
					Conditions = new SpawnConditions
					{
						NeighbourRequirements =
						{
							new NeighbourRequirement
							{
								TargetType = NeighbourTargetType.WaterFeature,
								TargetId = (int)WaterFeatureType.River,
								Radius = 3,
								MinCount = 1
							}
						},
						DensityModifiers =
						{
							new DensityModifier
							{
								TargetType = NeighbourTargetType.WaterFeature,
								TargetId = (int)WaterFeatureType.River,
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