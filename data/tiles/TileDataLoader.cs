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
					Density = 1f,
					MinPerTile = 10,
					MaxPerTile = 14
					// MinScale = 0.8f,
					// MaxScale = 1.2f
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