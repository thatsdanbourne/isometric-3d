extends CharacterBody3D

@onready var sprite: AnimatedSprite3D = $AnimatedSprite3D
@onready var hit_cooldown: Timer = $HitCooldown
@onready var hit_ray: RayCast3D = $HitRay
@onready var hotbar: Node = $Hotbar

const BASE_MAT := preload("res://resources/materials/WorldObjectBase.tres")

var speed := 4.0
var default_tool: Tool = preload("res://items/tools/fist/Fist.tres")
var equipped_item: Item
var can_swing := true

var current_biome := ""


func _ready():
	var mat = BASE_MAT.duplicate()
	mat.albedo_texture = sprite.sprite_frames.get_frame_texture("idle", 0)
	sprite.material_override = mat
	sprite.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_DOUBLE_SIDED

	hotbar.hotbar_changed.connect(_on_hotbar_changed)
	hotbar.selected_slot_changed.connect(_on_selected_slot_changed)


func _process(_delta: float):
	var biome = WorldUtils.get_biome_at_pos(global_position)
	if !biome.is_empty() and biome != current_biome:
		current_biome = biome
		WorldUtils.update_biome_tint(current_biome)


func get_active_tool() -> Tool:
	return equipped_item if equipped_item and equipped_item is Tool else default_tool


func use_active_tool():
	if not can_swing: return
	can_swing = false

	var tool = get_active_tool()
	if not tool: return

	AudioManager.play_random_at(tool.swing_sounds, global_position, AudioManager.BUS_TOOLS, 0.1, -12)

	hit_cooldown.start()

	if hit_ray.is_colliding():
		var target = hit_ray.get_collider()
		tool.use_on(target, self)


func _on_hit_cooldown_timeout() -> void:
	can_swing = true


func collect_item(item: Item, count: int):
	hotbar.add_item(item, count)


func _on_hotbar_changed(_hotbar_state):
	# Player reacts to hotbar change (e.g., equip a tool)
	pass

func _on_selected_slot_changed(_index):
	# Player reacts to changing selected item
	# e.g., update equipped tool mesh, or adjust attack damage.
	pass


func _unhandled_input(event: InputEvent):
	if event is InputEventMouseButton and event.is_pressed():
		if event.button_index == MOUSE_BUTTON_WHEEL_UP:
			hotbar.select_prev()
		elif event.button_index == MOUSE_BUTTON_WHEEL_DOWN:
			hotbar.select_next()


func _physics_process(_delta: float):
	var input_dir = Vector2(
		Input.get_action_strength("move_right") - Input.get_action_strength("move_left"),
		Input.get_action_strength("move_down") - Input.get_action_strength("move_up")
	).normalized()

	if input_dir != Vector2.ZERO:
		hit_ray.target_position = Vector3(input_dir.x, 0, input_dir.y) * 2.0
		var move_vec = Vector3(input_dir.x, 0, input_dir.y).rotated((Vector3(0, 1, 0)), deg_to_rad(45))
		velocity = move_vec * speed
		move_and_slide()
	else:
		velocity = Vector3.ZERO
		sprite.play("idle")

	if Input.is_action_pressed("use_tool"):
		use_active_tool()