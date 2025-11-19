extends Resource
class_name ObjectPlacementRule

@export var scene: PackedScene
@export var name: String

@export var base_noise_type: FastNoiseLite.NoiseType = FastNoiseLite.TYPE_SIMPLEX_SMOOTH
@export var base_noise_frequency: float = 0.01
@export var base_noise_threshold: float = 0.5
@export var base_noise_octaves: int = 5
@export var base_noise_gain: float = 0.5
@export var base_noise_lacunarity: float = 2.0

@export var use_detail_noise: bool = true
@export var detail_noise_type: FastNoiseLite.NoiseType = FastNoiseLite.TYPE_PERLIN
@export var detail_noise_frequency: float = 0.1
@export var detail_noise_threshold: float = 0.5
@export var detail_noise_octaves: int = 5
@export var detail_noise_gain: float = 0.5
@export var detail_noise_lacunarity: float = 2.0

var base_noise: FastNoiseLite
var detail_noise: FastNoiseLite

func init(terrain_seed):
	var combined_seed = terrain_seed + hash(scene.resource_path)
	base_noise = FastNoiseLite.new()
	base_noise.seed = combined_seed
	base_noise.noise_type = base_noise_type
	base_noise.frequency = base_noise_frequency
	base_noise.fractal_octaves = base_noise_octaves
	base_noise.fractal_gain = base_noise_gain
	base_noise.fractal_lacunarity = base_noise_lacunarity

	if use_detail_noise:
		detail_noise = FastNoiseLite.new()
		detail_noise.seed = combined_seed + 1
		detail_noise.noise_type = detail_noise_type
		detail_noise.frequency = detail_noise_frequency
		detail_noise.fractal_octaves = detail_noise_octaves
		detail_noise.fractal_gain = detail_noise_gain
		detail_noise.fractal_lacunarity = detail_noise_lacunarity


func should_place_at(x: int, z: int, density: float = 1.0) -> bool:
	density = clamp(density, 0.0, 1.0)
	if density <= 0.0: return false

	if randf() > density:
		return false

	var base_val = base_noise.get_noise_2d(x, z)
	if base_val < (base_noise_threshold * density):
		return false
	
	if use_detail_noise:
		var detail_val = detail_noise.get_noise_2d(x, z)
		if detail_val < (detail_noise_threshold * density):
			return false
	
	return true