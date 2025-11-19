extends Node3D

@onready var camera: Camera3D = $'../Camera3D'
@onready var player: CharacterBody3D = $WorldObjects/Player
@onready var world_objects: Node3D = $WorldObjects
@onready var gridmap: GridMap = $GridMap

@export var terrain_noise: FastNoiseLite
@export var grass_id: int = 0
@export var sand_id: int = 1

var terrain_seed := 0

var zoom_speed = 1.0
var last_size = -1.0

var is_thread_busy := false
var chunk_thread: Thread
var active_chunks: Dictionary = {}
var pending_loads: Array[Vector2i] = []
var _last_chunk_coord: Vector2i = Vector2i(-9999, -9999)
var placement_rules := PlacementRuleRegistry.get_all()


func _ready():
	if not terrain_noise:
		terrain_noise = FastNoiseLite.new()
		terrain_noise.noise_type = FastNoiseLite.TYPE_SIMPLEX_SMOOTH
		terrain_seed = randi()
		terrain_noise.seed = terrain_seed
		terrain_noise.frequency = 0.02
	
	PlacementRuleRegistry.initialise_rules(terrain_seed)


func _physics_process(_delta):
	if player:
		camera.position.x = lerp(camera.position.x, player.position.x + 30, 0.1)
		camera.position.z = lerp(camera.position.z, player.position.z + 30, 0.1)


func _process(_delta: float):
	if WorldUtils.world_to_chunk(player.global_position) != _last_chunk_coord:
		_update_active_chunks()


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

	var data: ChunkData = active_chunks[coord]
	
	for obj in data.objects:
		if is_instance_valid(obj):
			obj.queue_free()
	
	for tile in data.tiles:
		gridmap.set_cell_item(Vector3(tile.position.x, 0, tile.position.y), -1)
	
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
		var data = build_chunk_data(coord)
		call_deferred("_finalise_chunk_load", coord, data)
	
	is_thread_busy = false
	call_deferred("_cleanup_thread")


func build_chunk_data(coord) -> Dictionary:
	var tiles = []
	var objects = []

	for x in range(WorldUtils.CHUNK_SIZE):
		for y in range(WorldUtils.CHUNK_SIZE):
			var global_x = coord.x * WorldUtils.CHUNK_SIZE + x
			var global_y = coord.y * WorldUtils.CHUNK_SIZE + y

			var n = terrain_noise.get_noise_2d(global_x, global_y)
			var id = grass_id if n > 0.0 else sand_id

			tiles.append({ 
				"position": Vector2(global_x, global_y),
				"id": id
			})

			for rule in placement_rules:
				var base = rule.base_noise.get_noise_2d(global_x, global_y)
				if base <= rule.base_noise_threshold:
					continue
				
				if rule.use_detail_noise:
					var detail = rule.detail_noise.get_noise_2d(global_x, global_y)
					if detail <= rule.detail_noise_threshold:
						continue
				
				objects.append({
					"position": Vector3(global_x, 0, global_y) + Vector3(0.25, 0, 0.25),
					"rule": rule,
				})
	
	return { "tiles": tiles, "objects": objects }


func _finalise_chunk_load(coord: Vector2i, chunk_data: Dictionary):
	if active_chunks.has(coord): return

	var chunk := ChunkData.new()
	chunk.chunk_coord = coord
	chunk.tiles = chunk_data["tiles"]

	active_chunks[coord] = chunk

	for tile in chunk.tiles:
		gridmap.set_cell_item(Vector3i(tile.position.x, 0, tile.position.y), tile.id)
	
	for obj in chunk_data["objects"]:
		var rule: ObjectPlacementRule = obj["rule"]
		var scene := rule.scene.instantiate()
		if scene.has_method("initialise"):
			scene.initialise()
			
		scene.position = obj["position"]

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
