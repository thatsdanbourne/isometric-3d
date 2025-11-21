extends CanvasLayer

@onready var world: Node3D = $"../World"
@onready var fps: Label = $FPS
@onready var position: Label = $Position
@onready var biome: Label = $Biome
@onready var player: CharacterBody3D = $"../World/WorldObjects/Player"

var update_timer := 0.0

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	fps.position = Vector2(10, 10)
	position.position = Vector2(10, 70)
	biome.position = Vector2(10, 130)


# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	update_timer += delta
	if update_timer < 0.3:
		return
	update_timer = 0.0

	fps.text = str(Engine.get_frames_per_second()) + " fps"

	var p := player.position
	position.text = "x: " + str(int(p.x)) + ", y: " + str(int(p.z))

	biome.text = "Biome: " + player.current_biome.capitalize()
