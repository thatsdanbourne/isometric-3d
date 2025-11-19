extends Node3D
class_name WorldObject

@export var object_type := ""
@export var max_health := 3.0
@export_enum("wood", "stone") var hit_material: String = "wood"

static var MATERIAL_CACHE := {}
const BASE_MAT := preload("res://resources/materials/WorldObjectBase.tres")

var current_health := 0.0

func _ready():
    current_health = max_health
    _apply_sprite_material()
    translate(Vector3(0.0, 0.0, randf_range(-0.01, 0.01))) # apply slight z offset to prevent z-fighting

func cleanup():
    pass


func destroy():
    cleanup()
    queue_free()


func break_object():
    destroy()


func _apply_sprite_material():
    var sprite = _find_sprite()
    if sprite == null: return

    var tex = sprite.texture
    if tex == null: return

    var mat: StandardMaterial3D

    if MATERIAL_CACHE.has(tex):
        mat = MATERIAL_CACHE[tex]
    else:
        mat = BASE_MAT.duplicate()
        mat.albedo_texture = tex

        MATERIAL_CACHE[tex] = mat
    
    sprite.material_override = mat

    sprite.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF


func _find_sprite():
    if has_node("Sprite3D"):
        return get_node("Sprite3D")