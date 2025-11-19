extends Resource
class_name Item

@export var id: String
@export var name: String
@export var icon: Texture2D
@export var stack_size: int = 1
@export var description: String = ""

func use_on(_target: Node, _user: Node):
    pass
