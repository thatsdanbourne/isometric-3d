extends Node

const TILE_SIZE = 32
const CHUNK_SIZE := 16
const CHUNK_RADIUS := 3

const TILE_TYPES = [
	{
		"name": "grass",
		"variant_count": 2,
		"weights": [0.99, 0.01]
	},
	{
		"name": "sand",
		"variant_count": 2,
		"weights": [0.99, 0.01]
	},
	{
		"name": "snow",
		"variant_count": 2,
		"weights": [0.99, 0.01]
	},
]

var rng := RandomNumberGenerator.new()


func world_to_chunk(world_pos: Vector3) -> Vector2i:
	return Vector2i(
		floor(world_pos.x / CHUNK_SIZE),
		floor(world_pos.z / CHUNK_SIZE)
	)


func world_to_tile(world_pos: Vector3) -> Vector2i:
	return Vector2i(
		floor(world_pos.x),
		floor(world_pos.z)
	)


func get_biome(temp, humidity):
	for biome in PlacementRuleRegistry.biome_rules:
		if biome.matches(temp, humidity):
			return biome
	
	return PlacementRuleRegistry.biome_rules[0]


func pick_weighted_tile(tile_name: String) -> int:
	var i = TILE_TYPES.find(func(value): return value["name"] == tile_name)
	return rng.rand_weighted(TILE_TYPES[i]["weights"])


func get_tile_id(tile_name: String, variant: int) -> int:
	var id = 0
	for tile in TILE_TYPES:
		if tile["name"] == tile_name:
			return id + variant
		id += tile["variant_count"]
	
	return -1

