extends CanvasLayer

const Player = preload("res://scripts/player/Player.cs")

@onready var world: Node3D = $"../World"
@onready var fps: Label = $"MarginContainer/VBoxContainer/FPS"
@onready var position: Label = $"MarginContainer/VBoxContainer/Position"
@onready var biome: Label = $"MarginContainer/VBoxContainer/Biome"
@onready var player: Player = $"../World/WorldObjects/Player"

var update_timer := 0.0

func _process(delta: float) -> void:
	update_timer += delta
	if update_timer < 0.3:
		return
	update_timer = 0.0

	fps.text = str(Engine.get_frames_per_second()) + " fps"

	var p := player.position
	position.text = "x: " + str(int(p.x)) + ", y: " + str(int(p.z))

	biome.text = "Biome: " + player.CurrentBiome.capitalize()
