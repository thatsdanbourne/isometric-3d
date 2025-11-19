extends Resource
class_name BiomePlacementRule

@export var biome_name: String

@export var min_temperature: float = -1.0
@export var max_temperature: float = 1.0

@export var min_humidity: float = -1.0
@export var max_humidity: float = 1.0

@export var ground_tile_type: String = "grass"

@export var object_spawn_rules: Array[BiomeObjectSpawnRule] = []


func matches(temp: float, humidity: float) -> bool:
    return (
        temp >= min_temperature and temp <= max_temperature and
        humidity >= min_humidity and humidity <= max_humidity
    )