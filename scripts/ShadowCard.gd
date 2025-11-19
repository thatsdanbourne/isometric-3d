extends Sprite3D

var last_angle := 9999.0
var last_stretch := 9999.0

func _ready():
	add_to_group("ShadowCasters")

	var mat := StandardMaterial3D.new()
	mat.albedo_texture = texture
	mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA_SCISSOR
	mat.alpha_scissor_threshold = 0.1
	mat.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	mat.disable_receive_shadows = true
	cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_SHADOWS_ONLY
	material_override = mat

func apply_shadow(new_basis: Basis, stretch: float):
	# 1. Get the current global transform
	var xf = global_transform
	
	# 2. Update the rotation (Basis) from the cycle script
	xf.basis = new_basis
	
	# 3. Apply the stretch to the local scale (The safest way to change scale)
	# NOTE: You MUST set the scale locally, as the Basis is already rotated globally.
	scale.y = stretch 
	
	# 4. CRITICAL: The global_transform must be updated only once, but we don't 
	# need to reassign it since we modified the struct in step 2.
	# The combination of global_transform.basis = new_basis and scale = Vector3(..., stretch, ...) 
	# should be enough, but let's ensure we aren't creating a race condition.
	
	# If the above still jitters, simplify the transformation to ONLY rotation:
	
	global_transform.basis = new_basis
	scale.y = stretch