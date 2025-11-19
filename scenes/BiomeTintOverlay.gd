extends ColorRect

const BIOME_TINT_MAP = {
    "plains": {
		"color": Color(1.08, 1.02, 0.92),
		"strength": 0.3
	},
    "forest": {
		"color": Color(0.9, 1.1, 0.90),
		"strength": 0.4,
	},
    "taiga":  {
		"color": Color(0.82, 0.92, 1.18),
		"strength": 0.55,
	},
    "tundra": {
		"color": Color(0.88, 0.9, 1.20),
		"strength": 0.5,
	},
    "desert": {
		"color": Color(1.22, 1.12, 0.82),
		"strength": 0.7,
	},
}

var current_biome: String = ""
var target_tint: Color
var target_strength := 0.0
var fade_speed := 1.0


func _ready() -> void:
	WorldUtils.biome_tint_overlay = self


func set_tint_for_biome(biome: String):
	if biome == current_biome:
		return

	var data = BIOME_TINT_MAP.get(biome, null)

	if data:
		target_tint = data["color"]
		target_strength = data["strength"]

	current_biome = biome


func _process(delta):
	var tint = material.get_shader_parameter("biome_tint")
	tint = tint.lerp(target_tint, fade_speed * delta)
	material.set_shader_parameter("biome_tint", tint)

	var strength = material.get_shader_parameter("strength")
	strength = lerp(strength, target_strength, fade_speed * delta)
	material.set_shader_parameter("strength", strength)