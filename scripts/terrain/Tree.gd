extends WorldObject

@onready var sprite: Sprite3D = $Sprite3D

func take_damage(amount: float):
	current_health -= amount
	if current_health <= 0:
		break_object()


func break_object():
	var drop_scene = preload("res://scenes/ItemPickup.tscn")
	var pos := global_position

	for i in randi_range(2, 4):
		var drop = drop_scene.instantiate()
		drop.item = preload("res://items/resources/Wood.tres")
		drop.count = 1

		get_parent().add_child(drop)
		drop.global_position = pos
		drop.position.y += 0.2

	destroy()
