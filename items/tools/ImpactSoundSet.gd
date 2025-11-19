extends Resource
class_name ImpactSoundSet

@export var wood: Array[AudioStream] = []
@export var stone: Array[AudioStream] = []
@export var default: Array[AudioStream] = []


func get_sounds_for(material: String):
    match material:
        "wood": 
            if wood.size() > 0: return wood
        "stone":
            if stone.size() > 0: return stone
    
    return default