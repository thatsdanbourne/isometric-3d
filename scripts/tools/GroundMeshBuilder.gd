extends Node3D

@export var atlas: Texture2D
@export var tile_size: int = 32
@export var output_path: String = "res://tiles_meshlibrary.tres"

var grassOverlayAtlas: Texture2D = preload("res://assets/sprites/ground/overlays/grass_overlays.png")

var overlays_by_row := {
	TileType.GRASS: [Rect2i(0, 0, tile_size, tile_size)]
}

func _ready():
	if atlas == null:
		push_error("Assign a Texture2D atlas.")
		return

	var atlas_px := atlas.get_size()
	var tiles_x := int(atlas_px.x / tile_size)
	var tiles_y := int(atlas_px.y / tile_size)

	if tiles_x < 2:
		push_error("Atlas must have at least 2 columns (top + side).")
		return

	if tiles_y <= 0:
		push_error("Invalid atlas/tile size.")
		return

	print("Atlas:", atlas_px, " tiles:", tiles_x, "x", tiles_y)

	var lib := MeshLibrary.new()
	var id := 0

	for row in range(tiles_y):
		# --- extract base images ---
		var base_top_img := extract_image(atlas, top_rect(row))
		var base_side_img := extract_image(atlas, side_rect(row))

		var base_top_tex := ImageTexture.create_from_image(base_top_img)
		var base_side_tex := ImageTexture.create_from_image(base_side_img)

		# --- base tile ---
		var base_mesh := _make_tile_mesh(base_top_tex, base_side_tex)
		lib.create_item(id)
		lib.set_item_name(id, "tile_%d_base" % row)
		lib.set_item_mesh(id, base_mesh)
		id += 1

		# --- overlay variants (if any for this row) ---
		if overlays_by_row.has(row):
			for overlay_rect in overlays_by_row[row]:
				var overlay_img := extract_image(grassOverlayAtlas, overlay_rect)
				overlay_img = rotate_image_nearest(overlay_img, deg_to_rad(-45))

				var composed_top := compose_top_with_overlay(
					base_top_img,
					overlay_img
				)

				var composed_top_tex := ImageTexture.create_from_image(composed_top)

				var overlay_mesh := _make_tile_mesh(
					composed_top_tex,
					base_side_tex
				)

				lib.create_item(id)
				lib.set_item_name(id, "tile_%d_overlay_%d" % [row, id])
				lib.set_item_mesh(id, overlay_mesh)
				id += 1

	var ok := ResourceSaver.save(lib, output_path)
	if ok != OK:
		push_error("Failed to save MeshLibrary: %s" % output_path)
	else:
		print("✅ Saved MeshLibrary:", output_path)


func _make_tile_mesh(base_top_tex: Texture2D, side_tex: Texture2D) -> Mesh:
	var half := 0.5
	var h := 1.0

	# =====================
	# TOP SURFACE
	# =====================
	var eps := 0.001

	var top_verts := PackedVector3Array([
		Vector3(-half - eps, 0.0, -half - eps),
		Vector3( half + eps, 0.0, -half - eps),
		Vector3( half + eps, 0.0,  half + eps),
		Vector3(-half - eps, 0.0,  half + eps)
	])

	var top_norms := PackedVector3Array([
		Vector3.UP, Vector3.UP, Vector3.UP, Vector3.UP
	])

	var top_uvs := PackedVector2Array([
		Vector2(0, 0),
		Vector2(1, 0),
		Vector2(1, 1),
		Vector2(0, 1)
	])

	var top_indices := PackedInt32Array([0, 1, 2, 0, 2, 3])

	# =====================
	# SIDE SURFACE
	# =====================
	var side_verts := PackedVector3Array()
	var side_norms := PackedVector3Array()
	var side_uvs := PackedVector2Array()
	var side_indices := PackedInt32Array()

	# North
	_add_side(
		side_verts, side_norms, side_uvs, side_indices,
		Vector3(-half, 0, -half),
		Vector3( half, 0, -half),
		Vector3( half, -h, -half),
		Vector3(-half, -h, -half),
		Vector3(0, 0, -1)
	)

	# East
	_add_side(
		side_verts, side_norms, side_uvs, side_indices,
		Vector3( half, 0, -half),
		Vector3( half, 0,  half),
		Vector3( half, -h,  half),
		Vector3( half, -h, -half),
		Vector3(1, 0, 0)
	)

	# South
	_add_side(
		side_verts, side_norms, side_uvs, side_indices,
		Vector3( half, 0,  half),
		Vector3(-half, 0,  half),
		Vector3(-half, -h,  half),
		Vector3( half, -h,  half),
		Vector3(0, 0, 1)
	)

	# West
	_add_side(
		side_verts, side_norms, side_uvs, side_indices,
		Vector3(-half, 0,  half),
		Vector3(-half, 0, -half),
		Vector3(-half, -h, -half),
		Vector3(-half, -h,  half),
		Vector3(-1, 0, 0)
	)

	# =====================
	# BUILD MESH
	# =====================
	var mesh := ArrayMesh.new()

	mesh.add_surface_from_arrays(
		Mesh.PRIMITIVE_TRIANGLES,
		_make_arrays(top_verts, top_norms, top_uvs, top_indices)
	)

	mesh.add_surface_from_arrays(
		Mesh.PRIMITIVE_TRIANGLES,
		_make_arrays(side_verts, side_norms, side_uvs, side_indices)
	)

	# Materials

	var side_mat := StandardMaterial3D.new()
	side_mat.albedo_texture = side_tex
	side_mat.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
	side_mat.cull_mode = BaseMaterial3D.CULL_DISABLED

	var top_mat: Material
	var mat := StandardMaterial3D.new()
	mat.albedo_texture = base_top_tex
	mat.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_PER_PIXEL
	mat.cull_mode = BaseMaterial3D.CULL_DISABLED
	top_mat = mat

	mesh.surface_set_material(0, top_mat)
	mesh.surface_set_material(1, side_mat)

	return mesh


func compose_top_with_overlay(base: Image, overlay: Image) -> Image:
	if base.is_compressed():
		base.decompress()
	if overlay.is_compressed():
		overlay.decompress()

	var result := base.duplicate(true)
	for y in overlay.get_height():
		for x in overlay.get_width():
			var o := overlay.get_pixel(x, y)
			if o.a > 0.01:
				var b = result.get_pixel(x, y)
				result.set_pixel(x, y, b.blend(o))

	return result


func extract_image(tex: Texture2D, rect: Rect2i) -> Image:
	var img := tex.get_image()

	if img.is_compressed():
		img.decompress()

	var sub := Image.create(
		rect.size.x,
		rect.size.y,
		false,
		img.get_format()
	)

	sub.blit_rect(img, rect, Vector2i.ZERO)
	return sub


func _add_side(verts, norms, uvs, indices, a, b, c, d, normal):
	var start = verts.size()

	verts.append_array([a, b, c, d])
	for i in 4:
		norms.append(normal)

	uvs.append_array([
		Vector2(0, 0),
		Vector2(1, 0),
		Vector2(1, 1),
		Vector2(0, 1)
	])

	indices.append_array([
		start, start + 1, start + 2,
		start, start + 2, start + 3
	])


func _make_arrays(verts, norms, uvs, indices):
	var arr := []
	arr.resize(Mesh.ARRAY_MAX)
	arr[Mesh.ARRAY_VERTEX] = verts
	arr[Mesh.ARRAY_NORMAL] = norms
	arr[Mesh.ARRAY_TEX_UV] = uvs
	arr[Mesh.ARRAY_INDEX] = indices
	return arr


func rotate_image_nearest(src: Image, angle: float) -> Image:
	var w := src.get_width()
	var h := src.get_height()
	var cx := w * 0.5
	var cy := h * 0.5

	var dst := Image.create(w, h, false, src.get_format())
	dst.fill(Color(0, 0, 0, 0))

	var c := cos(angle)
	var s := sin(angle)

	for y in range(h):
		for x in range(w):
			var dx := x - cx
			var dy := y - cy

			var sx :=  c * dx + s * dy + cx
			var sy := -s * dx + c * dy + cy

			var ix := int(round(sx))
			var iy := int(round(sy))

			if ix >= 0 and ix < w and iy >= 0 and iy < h:
				dst.set_pixel(x, y, src.get_pixel(ix, iy))

	return dst


enum TileType {
	GRASS,
	SAND,
	SNOW
}

func top_rect(row: int) -> Rect2i:
	return Rect2i(
		0, 
		row * tile_size,
		tile_size,
		tile_size
	)

func side_rect(row: int) -> Rect2i:
	return Rect2i(
		tile_size, 
		row * tile_size,
		tile_size,
		tile_size
	)
