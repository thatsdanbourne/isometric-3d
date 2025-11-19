extends Node

var object_rules: Array[ObjectPlacementRule] = []
var biome_rules: Array[BiomePlacementRule] = []


func get_all_object_rules() -> Array[ObjectPlacementRule]:
    return object_rules


func get_all_biome_rules() -> Array[BiomePlacementRule]:
    return biome_rules


func initialise_object_rules(terrain_seed):
    for rule in object_rules:
        rule.init(terrain_seed)


func _ready():
    _load_rules_from_folder("res://resources/placement-rules/world-objects")
    _load_rules_from_folder("res://resources/placement-rules/biomes")
    print("Loaded %d object rules from placement rules" % object_rules.size())
    print("Loaded %d biome rules from placement rules" % biome_rules.size())


func _load_rules_from_folder(path: String):
    var dir := DirAccess.open(path)
    if dir == null:
        push_error("Could not open rule folder: %s" % path)
        return
    
    dir.list_dir_begin()

    while true:
        var file = dir.get_next()
        if file == "":
            break
        
        if dir.current_is_dir():
            continue
        
        if file.ends_with(".tres") or file.ends_with(".res"):
            var res_path = path + "/" + file
            var rule = load(res_path)

            if rule is ObjectPlacementRule:
                object_rules.append(rule)
            elif rule is BiomePlacementRule:
                biome_rules.append(rule)
            else:
                push_warning("Skipping non-rule resource: %s" % res_path)
        
    dir.list_dir_end()