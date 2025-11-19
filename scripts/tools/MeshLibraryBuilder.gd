extends Node3D

@export var atlas: Texture2D
@export var tile_size: Vector2i = Vector2i(32, 32)
@export var output_path: String = "res://tiles_meshlibrary.tres"

func _ready():
	if atlas == null:
		push_error("Assign a Texture2D atlas.")
		return

	var atlas_px := atlas.get_size()
	var tiles_x := int(atlas_px.x / tile_size.x)
	var tiles_y := int(atlas_px.y / tile_size.y)
	if tiles_x <= 0 or tiles_y <= 0:
		push_error("Invalid atlas/tile size.")
		return

	print("Atlas:", atlas_px, " tiles:", tiles_x, "x", tiles_y)

	var lib := MeshLibrary.new()
	var id := 0
	for y in range(tiles_y):
		for x in range(tiles_x):
			var mesh := _make_tile_mesh(atlas, x, y, tiles_x, tiles_y)
			lib.create_item(id)
			lib.set_item_name(id, "tile_%d_%d" % [x, y])
			lib.set_item_mesh(id, mesh)
			id += 1

	var ok := ResourceSaver.save(lib, output_path)
	if ok != OK:
		push_error("Failed to save MeshLibrary: %s" % output_path)
	else:
		print("✅ Saved MeshLibrary:", output_path)


func _make_tile_mesh(tex: Texture2D, tx: int, ty: int, tiles_x: int, tiles_y: int) -> Mesh:
	# Build a 1x1 quad in XZ plane (centered), with UVs set to the atlas region.
	# Vertices (counter-clockwise):
	var verts := PackedVector3Array([
		Vector3(-0.5, 0.0, -0.5),
		Vector3( 0.5, 0.0, -0.5),
		Vector3( 0.5, 0.0,  0.5),
		Vector3(-0.5, 0.0,  0.5),
	])
	var norms := PackedVector3Array([
		Vector3.UP, Vector3.UP, Vector3.UP, Vector3.UP
	])
	var u_step := 1.0 / float(tiles_x)
	var v_step := 1.0 / float(tiles_y)
	var u0 := tx * u_step
	var v0 := ty * v_step
	var u1 := u0 + u_step
	var v1 := v0 + v_step

	# Godot UV origin is top-left; with a typical atlas this is fine as-is.
	var uvs := PackedVector2Array([
		Vector2(u0, v0),
		Vector2(u1, v0),
		Vector2(u1, v1),
		Vector2(u0, v1),
	])
	var indices := PackedInt32Array([0, 1, 2, 0, 2, 3])

	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = verts
	arrays[Mesh.ARRAY_NORMAL] = norms
	arrays[Mesh.ARRAY_TEX_UV] = uvs
	arrays[Mesh.ARRAY_INDEX] = indices

	var mesh := ArrayMesh.new()
	var mat := StandardMaterial3D.new()
	mat.albedo_texture = tex
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_PER_PIXEL

	mat.enable_fog = true

	mat.transparency = BaseMaterial3D.TRANSPARENCY_DISABLED
	mat.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST

	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	mesh.surface_set_material(0, mat)

	return mesh
