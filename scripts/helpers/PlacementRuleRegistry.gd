extends Node

var rules: Array[ObjectPlacementRule] = []

func register(rule: ObjectPlacementRule):
    rules.append(rule)

func get_all() -> Array[ObjectPlacementRule]:
    return rules

func initialise_rules(terrain_seed):
    for rule in rules:
        rule.init(terrain_seed)

func _ready():
    _load_rules_from_folder("res://resources/placement-rules")
    print("Loaded %d object from placement rules" % rules.size())

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
                register(rule)
            else:
                push_warning("Skipping non-rule resource: %s" % res_path)
        
    dir.list_dir_end()