public enum BiomeId : ushort
{
	Unknown = 0,
	Plains = 1,
	Forest = 2,
	Desert = 3,
	Tundra = 4,
	Taiga = 5
}

public enum TileId : ushort
{
	Unknown = 0,
	Grass = 1,
	Water = 2,
	Sand = 3,
	Snow = 4
}

public enum ToolTier
{
	Fist,
	Stone,
	Copper,
	Iron,
	Steel
}

public enum StationType
{
	None,
	CraftingTable,
	Kiln,
	Smelter,
	Anvil
}

public enum WeatherType
{
	Clear,
	Rain,
	Snow
}

public enum ToolHitOutcome
{
	None,
	Failed,
	Hit,
	Destroyed
}