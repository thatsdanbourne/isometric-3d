extends Node3D

@onready var area: Area3D = $Area3D
@onready var sprite: Sprite3D = $Sprite3D
@onready var shadow: Sprite3D = $Shadow

const BASE_MAT := preload("res://resources/materials/WorldObjectBase.tres")

var item: Item
var count := 1
var magnet_speed_base: float = 6.0
var magnet_speed_max: float = 13.0
var magnet_enabled := false
var pickup_delay := 0.5
var collect_radius := 0.25

var velocity := Vector3.ZERO
var vertical_height := 0.0
var vertical_velocity := 0.0
var launch_strength := 6.0
var bounce_height := 1.2

var hover_phase := 0.0
var hover_amplitude := 0.15
var hover_speed := 3.0

var target: CharacterBody3D = null
var delay_timer := 0.0
var bouncing := true

var collected := false


func _ready():
	delay_timer = pickup_delay
	sprite.texture = item.icon

	var mat = BASE_MAT.duplicate()
	mat.albedo_texture = item.icon
	mat.billboard_mode = BaseMaterial3D.BILLBOARD_ENABLED
	sprite.material_override = mat

	sprite.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	sprite.pixel_size = 0.025

	shadow.texture = make_radial_shadow(128)
	shadow.position.y = 0.01
	shadow.rotation_degrees.x = -90

	var dir = Vector3(randf_range(-1.0, 1.0), 0.0, randf_range(-1.0, 1.0)).normalized()
	velocity = dir * launch_strength

	vertical_velocity = bounce_height * 8.0

	area.body_entered.connect(_on_body_entered)


func _on_body_entered(body):
	if collected: return

	if delay_timer <= 0 and body is CharacterBody3D:
		target = body

func _physics_process(delta: float):
	if collected: return

	if bouncing:
		_bounce_motion(delta)
	else:
		_hover_motion(delta)
	
	if !magnet_enabled:
		_update_sprite_offset()
		_update_shadow_scale()

	if delay_timer > 0:
		delay_timer -= delta
		
		if delay_timer <= 0:
			var bodies = $Area3D.get_overlapping_bodies()
			for body in bodies:
				if body is CharacterBody3D:
					target = body

	if target:
		_magnet_to_target(delta)
		return


func _bounce_motion(delta):
	global_position += velocity * delta
	velocity = velocity.lerp(Vector3.ZERO, delta * 3.0)

	vertical_velocity -= 20.0 * delta
	vertical_height += vertical_velocity * delta

	if vertical_height <= 0.0:
		vertical_height = 0.0
		bouncing = false


func _hover_motion(delta):
	hover_phase += delta * hover_speed
	vertical_height = sin(hover_phase) * hover_amplitude


func _magnet_to_target(delta):
	magnet_enabled = true
	bouncing = false

	# account for vertical_height offset
	sprite.position.y = lerp(sprite.position.y, 0.4, 0.05) 

	var target_pos = target.global_position

	var dist := sprite.global_position.distance_to(target_pos)
	var dist_factor = clamp(1.0 - dist / 10.0, 0.0, 1.0)
	var speed = lerp(magnet_speed_base, magnet_speed_max, dist_factor)

	global_position = global_position.lerp(
		target_pos,
		delta * speed
	)

	if global_position.distance_to(target.global_position) < collect_radius:
		_collect()
	
	return


func _collect():
	if collected: return
	collected = true

	$Area3D.monitoring = false
	$Area3D.monitorable = false

	target.collect_item(item, count)
	AudioManager.play_at(preload("res://assets/audio/sfx-pop.wav"), global_position, AudioManager.BUS_WORLD, 0.1, -12.0)

	var t := create_tween().set_parallel()
	t.tween_property(sprite, "pixel_size", 0.001, 0.1)
	t.tween_property(shadow, "scale", Vector3.ZERO, 0.1)

	await t.finished
	queue_free()


func _update_sprite_offset():
	sprite.position.y = vertical_height + 0.4


func _update_shadow_scale():
	var shadow_scale = clamp(1.0 - vertical_height * 0.7, 0.4, 1.0)
	shadow.scale = Vector3(shadow_scale, shadow_scale, shadow_scale)


func make_radial_shadow(size := 64) -> Texture2D:
	var img := Image.create(size, size, false, Image.FORMAT_RGBA8)
	for y in range(size):
		for x in range(size):
			var dx = float(x - size/2.0) / (size/2.0)
			var dy = float(y - size/2.0) / (size/2.0)
			var d = sqrt(dx*dx + dy*dy)
			var alpha = clamp(1.0 - d, 0.0, 1.0)
			img.set_pixel(x, y, Color(0, 0, 0, alpha * 0.5))

	var tex := ImageTexture.create_from_image(img)
	return tex