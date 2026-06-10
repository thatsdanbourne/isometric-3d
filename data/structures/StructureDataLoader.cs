using Godot;

public static class StructureDataLoader
{
	public static void LoadAllStructures()
	{
		StructureRegistry.Register(new StructureDefinition
		{
			Id = "plains_stone_circle",
			Size = new Vector2I(5, 5),
			Objects =
			{
				new StructureObject
				{
					LocalTile = new Vector2I(0, 0),
					Variants = { new SpawnVariant("standing_stone") }
				},
				new StructureObject
				{
					LocalTile = new Vector2I(5, 0),
					Variants = { new SpawnVariant("standing_stone") }
				},
				new StructureObject
				{
					LocalTile = new Vector2I(2, 2),
					Variants = { new SpawnVariant("chest") }
				},
				new StructureObject
				{
					LocalTile = new Vector2I(0, 5),
					Variants = { new SpawnVariant("standing_stone") }
				},
				new StructureObject
				{
					LocalTile = new Vector2I(1, 1),
					Chance = 0.7f,
					Variants = { new SpawnVariant("stone_floor") }
				},
				new StructureObject
				{
					LocalTile = new Vector2I(1, 2),
					Chance = 0.7f,
					Variants = { new SpawnVariant("stone_floor") }
				},
				new StructureObject
				{
					LocalTile = new Vector2I(1, 3),
					Chance = 0.7f,
					Variants = { new SpawnVariant("stone_floor") }
				},
				new StructureObject
				{
					LocalTile = new Vector2I(1, 4),
					Chance = 0.7f,
					Variants = { new SpawnVariant("stone_floor") }
				},
				new StructureObject
				{
					LocalTile = new Vector2I(2, 1),
					Chance = 0.7f,
					Variants = { new SpawnVariant("stone_floor") }
				},
				new StructureObject
				{
					LocalTile = new Vector2I(2, 2),
					Chance = 0.7f,
					Variants = { new SpawnVariant("stone_floor") }
				},
				new StructureObject
				{
					LocalTile = new Vector2I(2, 3),
					Chance = 0.7f,
					Variants = { new SpawnVariant("stone_floor") }
				},
				new StructureObject
				{
					LocalTile = new Vector2I(2, 4),
					Chance = 0.7f,
					Variants = { new SpawnVariant("stone_floor") }
				},
				new StructureObject
				{
					LocalTile = new Vector2I(3, 1),
					Chance = 0.7f,
					Variants = { new SpawnVariant("stone_floor") }
				},
				new StructureObject
				{
					LocalTile = new Vector2I(3, 2),
					Chance = 0.7f,
					Variants = { new SpawnVariant("stone_floor") }
				},
				new StructureObject
				{
					LocalTile = new Vector2I(3, 3),
					Chance = 0.7f,
					Variants = { new SpawnVariant("stone_floor") }
				},
				new StructureObject
				{
					LocalTile = new Vector2I(3, 4),
					Chance = 0.7f,
					Variants = { new SpawnVariant("stone_floor") }
				},
				new StructureObject
				{
					LocalTile = new Vector2I(4, 1),
					Chance = 0.7f,
					Variants = { new SpawnVariant("stone_floor") }
				},
				new StructureObject
				{
					LocalTile = new Vector2I(4, 2),
					Chance = 0.7f,
					Variants = { new SpawnVariant("stone_floor") }
				},
				new StructureObject
				{
					LocalTile = new Vector2I(4, 3),
					Chance = 0.7f,
					Variants = { new SpawnVariant("stone_floor") }
				},
				new StructureObject
				{
					LocalTile = new Vector2I(4, 4),
					Chance = 0.7f,
					Variants = { new SpawnVariant("stone_floor") }
				}
			}
		});
	}
}