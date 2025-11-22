extends Node3D

@export var atlas: Texture2D
@export var tile_size: Vector2i = Vector2i(32, 32)
@export var output_path: String = "res://tiles_meshlibrary.tres"

func _ready():
	if atlas == null:
		push_error("Assign a Texture2D atlas.")
		return

	var atlas_px := atlas.get_size()
	var tiles_x := int(atlas_px.x / tile_size.x) # should be 2
	var tiles_y := int(atlas_px.y / tile_size.y) # should be 3

	if tiles_x < 2:
		push_error("Atlas must have at least 2 columns (top + side).")
		return

	if tiles_y <= 0:
		push_error("Invalid atlas/tile size.")
		return

	print("Atlas:", atlas_px, " tiles:", tiles_x, "x", tiles_y)

	var lib := MeshLibrary.new()
	var id := 0

	# We create ONE item per row:
	# row 0: grass_top + grass_side
	# row 1: sand_top  + sand_side
	# row 2: snow_top  + snow_side
	for row in range(tiles_y):
		var mesh = _make_tile_mesh(atlas, row, tiles_x, tiles_y)
		lib.create_item(id)
		lib.set_item_name(id, "tile_row_%d" % row)
		lib.set_item_mesh(id, mesh)
		id += 1

	var ok := ResourceSaver.save(lib, output_path)
	if ok != OK:
		push_error("Failed to save MeshLibrary: %s" % output_path)
	else:
		print("✅ Saved MeshLibrary:", output_path)


func _make_tile_mesh(tex: Texture2D, row: int, tiles_x: int, tiles_y: int) -> Mesh:
	var half := 0.5
	var h := 1.0

	# --- VERTICES ---
	var verts := PackedVector3Array()

	# Top quad (y = 0)
	# 0: NW, 1: NE, 2: SE, 3: SW
	verts.append(Vector3(-half, 0.0, -half))
	verts.append(Vector3( half, 0.0, -half))
	verts.append(Vector3( half, 0.0,  half))
	verts.append(Vector3(-half, 0.0,  half))

	# Side quads (full height downwards)
	# North (-Z)
	verts.append(Vector3(-half, 0.0, -half))
	verts.append(Vector3( half, 0.0, -half))
	verts.append(Vector3( half, -h, -half))
	verts.append(Vector3(-half, -h, -half))

	# East (+X)
	verts.append(Vector3( half, 0.0, -half))
	verts.append(Vector3( half, 0.0,  half))
	verts.append(Vector3( half, -h,  half))
	verts.append(Vector3( half, -h, -half))

	# South (+Z)
	verts.append(Vector3( half, 0.0,  half))
	verts.append(Vector3(-half, 0.0,  half))
	verts.append(Vector3(-half, -h,  half))
	verts.append(Vector3( half, -h,  half))

	# West (-X)
	verts.append(Vector3(-half, 0.0,  half))
	verts.append(Vector3(-half, 0.0, -half))
	verts.append(Vector3(-half, -h, -half))
	verts.append(Vector3(-half, -h,  half))

	# --- NORMALS ---
	var norms := PackedVector3Array()

	# Top
	for i in range(4):
		norms.append(Vector3.UP)

	# North
	for i in range(4):
		norms.append(Vector3(0, 0, -1))

	# East
	for i in range(4):
		norms.append(Vector3(1, 0, 0))

	# South
	for i in range(4):
		norms.append(Vector3(0, 0, 1))

	# West
	for i in range(4):
		norms.append(Vector3(-1, 0, 0))

	# --- UVs ---
	var u_step := 1.0 / float(tiles_x) # 0..1 horizontally
	var v_step := 1.0 / float(tiles_y) # 0..1 vertically

	var top_tx := 0 # column 0 = top
	var side_tx := 1 # column 1 = side

	var top_u0 := float(top_tx) * u_step
	var top_v0 := float(row) * v_step
	var top_u1 := top_u0 + u_step
	var top_v1 := top_v0 + v_step

	var side_u0 := float(side_tx) * u_step
	var side_v0 := float(row) * v_step
	var side_u1 := side_u0 + u_step
	var side_v1 := side_v0 + v_step

	var uvs := PackedVector2Array()

	# Top UVs (match full top tile region)
	uvs.append(Vector2(top_u0, top_v0)) # 0
	uvs.append(Vector2(top_u1, top_v0)) # 1
	uvs.append(Vector2(top_u1, top_v1)) # 2
	uvs.append(Vector2(top_u0, top_v1)) # 3

	# North side UVs
	uvs.append(Vector2(side_u0, side_v0))
	uvs.append(Vector2(side_u1, side_v0))
	uvs.append(Vector2(side_u1, side_v1))
	uvs.append(Vector2(side_u0, side_v1))

	# East side UVs
	uvs.append(Vector2(side_u0, side_v0))
	uvs.append(Vector2(side_u1, side_v0))
	uvs.append(Vector2(side_u1, side_v1))
	uvs.append(Vector2(side_u0, side_v1))

	# South side UVs
	uvs.append(Vector2(side_u0, side_v0))
	uvs.append(Vector2(side_u1, side_v0))
	uvs.append(Vector2(side_u1, side_v1))
	uvs.append(Vector2(side_u0, side_v1))

	# West side UVs
	uvs.append(Vector2(side_u0, side_v0))
	uvs.append(Vector2(side_u1, side_v0))
	uvs.append(Vector2(side_u1, side_v1))
	uvs.append(Vector2(side_u0, side_v1))

	# --- INDICES ---
	var indices := PackedInt32Array()

	# Top face (0..3)
	indices.append_array([0, 1, 2, 0, 2, 3])

	# Each side face: 4 verts each
	for face in range(4):
		var base := 4 + face * 4
		indices.append_array([
			base + 0, base + 1, base + 2,
			base + 0, base + 2, base + 3
		])

	# --- BUILD MESH ---
	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = verts
	arrays[Mesh.ARRAY_NORMAL] = norms
	arrays[Mesh.ARRAY_TEX_UV] = uvs
	arrays[Mesh.ARRAY_INDEX] = indices

	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)

	var mat := StandardMaterial3D.new()
	mat.albedo_texture = tex
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_PER_PIXEL
	mat.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
	mat.transparency = BaseMaterial3D.TRANSPARENCY_DISABLED
	mat.cull_mode = BaseMaterial3D.CULL_DISABLED

	mesh.surface_set_material(0, mat)
	return mesh
