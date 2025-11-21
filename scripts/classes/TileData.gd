extends RefCounted
class_name Tile

var id: int
var biome: String
var temp: float
var humidity: float

func _init(init_id := 0, init_biome := "", init_temp := 0.0, init_humidity := 0.0):
    self.id = init_id
    self.biome = init_biome
    self.temp = init_temp
    self.humidity = init_humidity