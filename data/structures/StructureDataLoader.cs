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
				new StructureObject { ObjectId = "standing_stone", LocalTile = new Vector2I(0, 0) },
				new StructureObject { ObjectId = "standing_stone", LocalTile = new Vector2I(5, 0) },
				new StructureObject { ObjectId = "chest", LocalTile = new Vector2I(3, 3) },
				new StructureObject { ObjectId = "standing_stone", LocalTile = new Vector2I(0, 5) }
			}
		});
	}
}