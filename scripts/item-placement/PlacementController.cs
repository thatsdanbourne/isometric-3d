using Godot;

public partial class PlacementController : Node
{
	private static readonly PackedScene PlacementPreviewScene =
		GD.Load<PackedScene>("res://scenes/placeables/PlacementPreview.tscn");

	private World _world;
	private Player _player;

	private bool _active;
	private PlaceableItem _placeable;
	private PlacementPreview _preview;

	public bool Active => _active;

	public void Init(World world, Player player)
	{
		_world = world;
		_player = player;
	}

	public void Enter(PlaceableItem placeable)
	{
		if (_active && _placeable == placeable) return;

		_active = true;
		_placeable = placeable;

		EnsurePreview();
		_preview.Init(_world, placeable);
	}

	public void Exit()
	{
		if (!_active) return;

		_active = false;
		_placeable = null;

		RemovePreview();
	}

	public void Tick(Camera3D camera, bool shouldBeVisible)
	{
		if (!_active) return;
		if (_preview == null) return;

		_preview.Tick(camera, shouldBeVisible);
	}

	public bool TryPlace()
	{
		if (!_active || _placeable == null || _preview == null) return false;

		if (!_preview.HasTile || !_preview.CanPlaceCurrent) return false;

		var tile = _preview.CurrentTile;

		if (!_world.PlaceItem(tile, _placeable)) return false;

		InventoryManager.Instance.RemoveItem(_player, _placeable, 1);
		return true;
	}

	private void EnsurePreview()
	{
		if (_preview != null && _preview.IsInsideTree()) return;

		_preview?.QueueFree();
		_preview = PlacementPreviewScene.Instantiate<PlacementPreview>();

		GetTree().CurrentScene.CallDeferred(Node.MethodName.AddChild, _preview);
	}

	private void RemovePreview()
	{
		if (_preview == null) return;

		if (_preview.IsInsideTree())
			_preview.QueueFree();

		_preview = null;
	}
}