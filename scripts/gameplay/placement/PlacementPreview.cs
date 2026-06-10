using Godot;
using System.Collections.Generic;

public partial class PlacementPreview : Node3D
{
	private Node3D _previewInstance;
	private readonly List<StandardMaterial3D> _previewMaterials = new();

	private World _world;
	private PlaceableItem _placeable;

	private Vector2I _lastTile;
	private bool _hasLastTile;

	public Vector2I CurrentTile => _hasLastTile ? _lastTile : default;
	public bool CanPlaceCurrent { get; private set; }
	public bool HasTile => _hasLastTile;

	public void Init(World world, PlaceableItem placeable)
	{
		_world = world;
		_placeable = placeable;

		SetPreviewScene(placeable.PreviewScene);
		_hasLastTile = false;
	}

	public Vector2I Tick(Camera3D camera, bool shouldBeVisible)
	{
		if (!IsInsideTree()) return _hasLastTile ? _lastTile : default;

		Visible = shouldBeVisible;

		if (!shouldBeVisible || camera == null || _world == null || _placeable == null)
			return _hasLastTile ? _lastTile : default;

		var tile = TileUtils.GetMouseTilePosition(camera);

		if (!_hasLastTile && tile == _lastTile) return tile;

		_lastTile = tile;
		_hasLastTile = true;

		GlobalPosition = TileUtils.TileToWorld(tile);

		CanPlaceCurrent = _world.CanPlace(tile, _placeable);
		SetValid(CanPlaceCurrent);

		return tile;
	}

	private void SetPreviewScene(PackedScene scene)
	{
		ClearPreview();
		_previewInstance = scene.Instantiate<Node3D>();
		AddChild(_previewInstance);
		_previewInstance.RotationDegrees = new Vector3(0f, -45f, 0f);

		ApplyPreviewMaterial(_previewInstance);
	}

	private void ClearPreview()
	{
		if (_previewInstance != null)
		{
			_previewInstance.QueueFree();
			_previewInstance = null;
		}

		_previewMaterials.Clear();
	}

	private void SetValid(bool valid)
	{
		var color = valid
			? new Color(0.3f, 1f, 0.3f, 0.6f)
			: new Color(1f, 0.3f, 0.3f, 0.6f);

		foreach (var mat in _previewMaterials)
			mat.AlbedoColor = color;
	}

	private void ApplyPreviewMaterial(Node node)
	{
		if (node is MeshInstance3D mesh)
		{
			mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

			var mat = new StandardMaterial3D
			{
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				AlbedoColor = new Color(0.3f, 1f, 0.3f, 0.6f),
				CullMode = BaseMaterial3D.CullModeEnum.Disabled,
				RenderPriority = 2
			};

			mesh.MaterialOverride = mat;
			_previewMaterials.Add(mat);
		}

		foreach (var child in node.GetChildren())
			ApplyPreviewMaterial(child);
	}
}