extends CanvasLayer

@onready var fps: Label = $FPS
@onready var position: Label = $Position
@onready var player: CharacterBody3D = $"../World/WorldObjects/Player"

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	fps.position = Vector2(10, 10)
	position.position = Vector2(10, 70)


# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(_delta: float) -> void:
	fps.text = "%sfps" % Engine.get_frames_per_second()
	position.text = "x: %s, y: %s" % [int(player.position.x), int(player.position.z)]
