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

var world: Node = null
var biome_tint_overlay: Node = null

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


func tile_to_chunk(tile_pos: Vector2i) -> Vector2i:
	return Vector2i(
		floor(tile_pos.x / float(CHUNK_SIZE)),
		floor(tile_pos.y / float(CHUNK_SIZE))
	)


func tile_local_in_chunk(tile_pos: Vector2i) -> Vector2i:
	return Vector2i(
		abs(tile_pos.x % CHUNK_SIZE),
		abs(tile_pos.y % CHUNK_SIZE)
	)


func get_biome_at_pos(world_pos: Vector3) -> String:
	var tile = WorldUtils.world_to_tile(world_pos)
	var chunk_coord = WorldUtils.tile_to_chunk(tile)
	var local = WorldUtils.tile_local_in_chunk(tile)

	var chunk = world.active_chunks.get(chunk_coord)
	if chunk == null:
		return ""
	
	return chunk.tiles[local.x][local.y]["biome"]


func get_biome(temp, humidity):
	for biome in PlacementRuleRegistry.biome_rules:
		if biome.matches(temp, humidity):
			return biome
	
	return PlacementRuleRegistry.biome_rules[0]


func update_biome_tint(biome: String):
	if biome_tint_overlay:
		biome_tint_overlay.set_tint_for_biome(biome)



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

