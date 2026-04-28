public enum BiomeId : ushort
{
	Unknown = 0,
	Plains = 1,
	Forest = 2,
	Desert = 3,
	Tundra = 4,
	Taiga = 5,
	River = 6,
	Riverbank = 7,
	Lake = 8,
	LakeShore = 9
}

public enum BiomeKind
{
	Base,
	Overlay
}

public enum WaterFeatureType
{
	None,
	River,
	RiverBank,
	Lake,
	LakeShore
}

public enum ObjectSpawnPass
{
	LargeObjects,
	GroundPickups,
	Decor
}

public readonly record struct WaterFeatureResult(
	WaterFeatureType Type,
	BiomeDefinition BiomeOverride
);

public enum NeighbourTargetType
{
	WaterFeature,
	Object
}

public enum DistanceFalloffMode
{
	None,
	Linear,
	Inverse
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

public enum ContainerKind
{
	Inventory = 0,
	Hotbar = 1,
	Storage = 2
}

public enum GlobalWeatherType
{
	Clear,
	Precipitation
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

public enum InventorySfxAction
{
	Pickup,
	Drop
}

// debug enums
public enum SessionMode
{
	None,
	Menu,
	Single,
	Host,
	Client,
	Server
}

public enum WorldLoadMode
{
	Random,
	Seed,
	Save
}

public enum MobState
{
	Idle,
	Wander,
	Chase,
	Attack,
	Dead
}