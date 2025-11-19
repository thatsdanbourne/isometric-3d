extends Item
class_name Tool

@export var power := 1.0
@export var durability := 100
@export var effectiveness: Dictionary = {}
@export var swing_sounds: Array[AudioStream] = []
@export var impact_sounds: ImpactSoundSet


func use_on(target: Node, user: Node):
    if not target is WorldObject: return

    var material = target.hit_material
    var sounds = impact_sounds.get_sounds_for(material)

    if not sounds.is_empty():
        await user.get_tree().create_timer(0.1).timeout
        AudioManager.play_random_at(sounds, target.global_position, AudioManager.BUS_TOOLS, 0.1)
    

    if target.has_method("take_damage"):
        var object_type = ""

        if "object_type" in target:
            object_type = target.object_type
        
        var multiplier = effectiveness.get(object_type, 1.0)
        var total_damage = power * multiplier
        target.take_damage(total_damage)
