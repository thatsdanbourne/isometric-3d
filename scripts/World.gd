extends Node3D

@onready var camera: Camera3D = $'../Camera3D'
@onready var player: CharacterBody3D = $WorldObjects/Player
@onready var world_objects: Node3D = $WorldObjects
@onready var gridmap: GridMap = $GridMap

# @export var terrain_noise: FastNoiseLite
@export var temp_noise: FastNoiseLite
@export var humidity_noise: FastNoiseLite

@export var grass_id: int = 0
@export var sand_id: int = 1

var terrain_seed := 0

var zoom_speed = 1.0
var last_size = -1.0

var is_thread_busy := false
var chunk_thread: Thread
var active_chunks: Dictionary[Vector2i, Chunk] = {}
var pending_loads: Array[Vector2i] = []
var _last_chunk_coord: Vector2i = Vector2i(-9999, -9999)
var placement_rules := PlacementRuleRegistry.get_all_object_rules()

var perf_last_time := 0.0
var perf_last_ms := 0.0


func _ready():
	WorldUtils.world = self
	terrain_seed = randi()

	# if not terrain_noise:
	# 	terrain_noise = FastNoiseLite.new()
	# 	terrain_noise.noise_type = FastNoiseLite.TYPE_SIMPLEX_SMOOTH
	# 	terrain_noise.seed = terrain_seed
	# 	terrain_noise.frequency = 0.02
	
	if not temp_noise:
		temp_noise = FastNoiseLite.new()
		temp_noise.noise_type = FastNoiseLite.TYPE_PERLIN
		temp_noise.seed = terrain_seed + 1000
		temp_noise.frequency = 0.0015
		temp_noise.fractal_octaves = 3
		temp_noise.fractal_gain = 0.5
		temp_noise.fractal_lacunarity = 2.0

	if not humidity_noise:
		humidity_noise = FastNoiseLite.new()
		humidity_noise.noise_type = FastNoiseLite.TYPE_PERLIN
		humidity_noise.seed = terrain_seed + 2000
		humidity_noise.frequency = 0.003
		humidity_noise.fractal_octaves = 4
		humidity_noise.fractal_gain = 0.55
		humidity_noise.fractal_lacunarity = 2.0
	
	PlacementRuleRegistry.initialise_object_rules(terrain_seed)


func _physics_process(_delta):
	if player:
		camera.position.x = lerp(camera.position.x, player.position.x + 30, 0.1)
		camera.position.z = lerp(camera.position.z, player.position.z + 30, 0.1)


func _process(_delta: float):
	var chunk = WorldUtils.world_to_chunk(player.global_position)
	if chunk != _last_chunk_coord:
		_update_active_chunks()
		_last_chunk_coord = chunk

	
func _update_active_chunks():
	var player_chunk = WorldUtils.world_to_chunk(player.global_position)

	for coord in active_chunks.keys().duplicate():
		if abs(coord.x - player_chunk.x) > WorldUtils.CHUNK_RADIUS or abs(coord.y - player_chunk.y) > WorldUtils.CHUNK_RADIUS:
			unload_chunk(coord)
	
	for x in range(-WorldUtils.CHUNK_RADIUS, WorldUtils.CHUNK_RADIUS + 1):
		for y in range(-WorldUtils.CHUNK_RADIUS, WorldUtils.CHUNK_RADIUS + 1):
			var target_coord = player_chunk + Vector2i(x, y)
			if not active_chunks.has(target_coord):
				pending_loads.append(target_coord)
	
	if not is_thread_busy and not pending_loads.is_empty():
		_start_chunk_thread()


func unload_chunk(coord: Vector2i):
	if not active_chunks.has(coord): return

	var C := WorldUtils.CHUNK_SIZE
	var data: Chunk = active_chunks[coord]
	
	for obj in data.objects:
		if is_instance_valid(obj):
			obj.queue_free()
	
	var pos := Vector3i()

	for x in range(C):
		for y in range(C):
			var global_x = coord.x * C + x
			var global_y = coord.y * C + y
			pos.x = global_x
			pos.y = 0
			pos.z = global_y
			gridmap.set_cell_item(pos, -1)
	
	active_chunks.erase(coord)


func _start_chunk_thread():
	if chunk_thread and chunk_thread.is_alive():
		return
	
	is_thread_busy = true
	chunk_thread = Thread.new()
	chunk_thread.start(Callable(self, "_generate_chunks_thread"))


func _generate_chunks_thread():
	while not pending_loads.is_empty():
		var coord = pending_loads.pop_front()
		perf_stamp()
		var data = build_chunk_data(coord)
		perf_end("Build Chunk " + str(coord))

		perf_stamp()
		call_deferred("_finalise_chunk_load", coord, data)
		perf_end("Finalise Chunk " + str(coord))
	
	is_thread_busy = false
	call_deferred("_cleanup_thread")


func build_chunk_data(coord: Vector2i) -> Dictionary:
	var C := WorldUtils.CHUNK_SIZE

	var tiles: Array[Array] = []
	tiles.resize(C)
	for x in range(C):
		tiles[x] = []
		tiles[x].resize(C)

	var objects: Array = []

	var base_x = coord.x * C
	var base_y = coord.y * C
	
	for x in range(C):
		for y in range(C):
			var global_x = base_x + x
			var global_y = base_y + y
			var temp_raw = temp_noise.get_noise_2d(global_x, global_y)
			var humidity_raw = humidity_noise.get_noise_2d(global_x, global_y)

			var temp = adjust_contrast((temp_raw + 1.0) * 0.5)
			var humidity = adjust_contrast((humidity_raw + 1.0) * 0.5)

			var biome: BiomePlacementRule = WorldUtils.get_biome(temp, humidity)
			var tile_type = biome.ground_tile_type
			var tile_variant = WorldUtils.pick_weighted_tile(tile_type)
			var tile_id = WorldUtils.get_tile_id(tile_type, tile_variant)

			var tile = Tile.new(tile_id, biome.biome_name, temp, humidity) 
			tiles[x][y] = tile

			var spawn_rules_list = biome.object_spawn_rules
			for spawn_rules in spawn_rules_list:
				var rule = spawn_rules.rule
				var density = spawn_rules.spawn_density

				if rule.should_place_at(global_x, global_y, density):
					var pos = Vector3(global_x + 0.25, 0, global_y + 0.25)
					var object = ChunkObject.new()
					object.position = pos
					object.rule = rule
					objects.append(object)
	
	return { "tiles": tiles, "objects": objects }


func adjust_contrast(v: float, amount := 1.75) -> float:
	return clamp((v - 0.5) * amount + 0.5, 0.0, 1.0)


func _finalise_chunk_load(coord: Vector2i, chunk_data: Dictionary):
	if active_chunks.has(coord): return

	var C := WorldUtils.CHUNK_SIZE

	var chunk := Chunk.new()
	chunk.chunk_coord = coord
	chunk.tiles = chunk_data["tiles"]

	active_chunks[coord] = chunk

	var pos := Vector3i()

	var base_x = coord.x * C
	var base_y = coord.y * C

	for x in range(C):
		for y in range(C):
			var tile = chunk.tiles[x][y]
			if tile == null:
				continue

			var tile_id = tile.id

			var global_x = base_x + x
			var global_y = base_y + y
			pos.x = global_x
			pos.y = 0
			pos.z = global_y

			gridmap.set_cell_item(pos, tile_id)
	
	for obj in chunk_data["objects"]:
		var rule: ObjectPlacementRule = obj.rule
		var scene := rule.scene.instantiate()
		if scene.has_method("initialise"):
			scene.initialise()
			
		scene.position = obj.position

		world_objects.add_child(scene)
		chunk.objects.append(scene)


func _cleanup_thread():
	if chunk_thread:
		chunk_thread.wait_to_finish()
		chunk_thread = null


func _input(_event):
	if Input.is_action_just_pressed("zoom_in"):
		camera.size = max(camera.size - 2, 5)
	elif Input.is_action_just_pressed("zoom_out"):
		camera.size = min(camera.size + 2, 200)

func perf_stamp():
	perf_last_time = Time.get_ticks_usec()

func perf_end(tag: String):
	var now := Time.get_ticks_usec()
	perf_last_ms = float(now - perf_last_time) / 1000.0
	print(tag, ": ", perf_last_ms, " ms")