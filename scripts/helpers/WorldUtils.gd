extends Node

const TILE_SIZE = 32
const CHUNK_SIZE := 16
const CHUNK_RADIUS := 3

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