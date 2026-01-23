public static class TileDataLoader
{
    public static void LoadAllTiles()
    {
        TileRegistry.Register(new TileDefinition
        {
            Name = "grass",
            GridTileId = 0,
        });

        TileRegistry.Register(new TileDefinition
        {
            Name = "sand",
            GridTileId = 3,
        });

        TileRegistry.Register(new TileDefinition
        {
            Name = "snow",
            GridTileId = 4,
        });

        TileRegistry.Register(new TileDefinition
        {
            Name = "water",
            GridTileId = 0, // water on different gridmap
            IsWater = true,
        });
    }
}
