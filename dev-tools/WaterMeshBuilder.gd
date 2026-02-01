extends Node3D

@export var water_texture: Texture2D
@export var output_path: String = "res://water_meshlibrary.tres"

func _ready():
	if water_texture == null:
		push_error("Assign a water texture.")
		return

	var lib := MeshLibrary.new()
	var mesh := _make_water_mesh()

	lib.create_item(0)
	lib.set_item_name(0, "water")
	lib.set_item_mesh(0, mesh)

	var ok := ResourceSaver.save(lib, output_path)
	if ok != OK:
		push_error("Failed to save water MeshLibrary")
	else:
		print("💧 Saved Water MeshLibrary:", output_path)


func _make_water_mesh() -> Mesh:
	var half := 0.5

	# Quad vertices (XZ plane)
	var verts := PackedVector3Array([
		Vector3(-half, 0, -half),
		Vector3( half, 0, -half),
		Vector3( half, 0,  half),
		Vector3(-half, 0,  half)
	])

	var norms := PackedVector3Array([
		Vector3.UP, Vector3.UP, Vector3.UP, Vector3.UP
	])

	var uvs := PackedVector2Array([
		Vector2(0,0),
		Vector2(1,0),
		Vector2(1,1),
		Vector2(0,1),
	])

	var indices := PackedInt32Array([0,1,2, 0,2,3])

	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = verts
	arrays[Mesh.ARRAY_NORMAL] = norms
	arrays[Mesh.ARRAY_TEX_UV] = uvs
	arrays[Mesh.ARRAY_INDEX] = indices

	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)

	# Material
	var mat := ShaderMaterial.new()
	mat.shader = load("res://resources/shaders/Water.gdshader")
	mat.set_shader_parameter("water_tex", water_texture)

	mesh.surface_set_material(0, mat)

	return mesh