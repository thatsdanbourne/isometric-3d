extends Node

signal hotbar_changed(hotbar)
signal selected_slot_changed(index)

var hotbar := []
var selected_index := 0
var slot_count := 8


func _ready():
    hotbar.resize(slot_count)
    for i in range(slot_count):
        hotbar[i] = null


func select_next():
    selected_index = (selected_index + 1) % slot_count
    emit_signal("selected_slot_changed", selected_index)


func select_prev():
    selected_index = (selected_index - 1 + slot_count) % slot_count
    emit_signal("selected_slot_changed", selected_index)


func add_item(item: Item, amount: int) -> bool:
    var remaining := amount

    # merge with existing stacks
    for i in range(slot_count):
        var data = hotbar[i]
        if data and data.item == item:
            var free = item.stack_size - data.count
            if free > 0:
                var add = min(free, remaining)
                data.count += add
                remaining -= add
                emit_signal("hotbar_changed", hotbar)
                if remaining <= 0: return true

    # fill new slots
    for i in range(slot_count):
        if hotbar[i] == null:
            var add = min(item.stack_size, remaining)
            hotbar[i] = {"item": item, "count": add}
            remaining -= add
            emit_signal("hotbar_changed", hotbar)
            if remaining <= 0: return true

    return remaining < amount