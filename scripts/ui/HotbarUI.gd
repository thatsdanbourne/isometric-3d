extends HBoxContainer

@onready var slot_style = preload("res://scenes/ui/HotbarSlotStyle.tres")
@onready var slot_scene = preload("res://scenes/ui/HotbarSlot.tscn")

var slot_nodes: Array[Panel] = []
var slot_count := 8

var player_ref


func bind_player(player):
	player_ref = player
	slot_count = player.hotbar.size()

	_create_slots()

	player.hotbar_changed.connect(_update_slots)
	player.selected_slot_changed.connect(_on_selected_slot_changed)

	_update_slots(player.hotbar)
	_on_selected_slot_changed(player.selected_index)


func _create_slots():
	slot_nodes.clear()

	for i in range(slot_count):
		var slot_node: Panel = slot_scene.instantiate()
		slot_node.add_theme_stylebox_override("panel", slot_style)
		slot_node.pivot_offset = slot_node.size / 2.0
		add_child(slot_node)
		slot_nodes.append(slot_node)


func _update_slots(hotbar):
	for i in range(slot_nodes.size()):
		var slot = slot_nodes[i]
		var data = hotbar[i]

		var icon = slot.get_node("MarginContainer/VBoxContainer/Icon")
		var label = slot.get_node("MarginContainer/VBoxContainer/Icon/Label")

		if data:
			icon.texture = data.item.icon
			label.text = str(data.count)
		else:
			icon.texture = null
			label.text = ""


func _on_selected_slot_changed(index):
	for i in range(slot_nodes.size()):
		slot_nodes[i].modulate = Color(1, 1, 1) if i == index else Color(0.6, 0.6, 0.6)
	
	_animate_selection(index)


func _animate_selection(index):
	if index < 0 or index >= slot_nodes.size(): return

	var slot = slot_nodes[index]
	var tween = create_tween().set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_OUT)
	slot.scale = Vector2.ONE
	tween.tween_property(slot, "scale", Vector2(1.12, 1.12), 0.1)
	tween.tween_property(slot, "scale", Vector2.ONE, 0.1)
