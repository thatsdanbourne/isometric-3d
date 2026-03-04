public static class TileDataLoader
{
	public static void LoadAllTiles()
	{
		TileRegistry.Register(new TileDefinition
		{
			Id = TileId.Grass,
			Name = "grass",
			GridTileId = 0,
			DetailMeshes =
			{
				new DetailMeshRule
				{
					MeshId = "grass",
					Density = 0.8f,
					MinPerTile = 10,
					MaxPerTile = 15
				}
			}
		});

		TileRegistry.Register(new TileDefinition
		{
			Id = TileId.Sand,
			Name = "sand",
			GridTileId = 3
		});

		TileRegistry.Register(new TileDefinition
		{
			Id = TileId.Snow,
			Name = "snow",
			GridTileId = 4
		});

		TileRegistry.Register(new TileDefinition
		{
			Id = TileId.Water,
			Name = "water",
			GridTileId = 0, // water on different gridmap
			IsWater = true
		});
	}
}