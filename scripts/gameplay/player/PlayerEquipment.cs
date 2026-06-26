using Godot;

public partial class PlayerEquipment : Node
{
	[Export] public NodePath RightHandAttachPath;
	[Export] public NodePath LeftHandAttachPath;

	private Node3D _rightHandAttach;
	private Node3D _currentRightHandTool;
	private Node3D _leftHandAttach;
	private Node3D _currentLeftHandTool;
	private ToolItem _defaultTool;
	private Item _heldItem;
	private Item _lastHeldItem;
	private ToolItem _offhandTool;

	public override void _Ready()
	{
		_rightHandAttach = GetNode<Node3D>(RightHandAttachPath);
		_leftHandAttach = GetNode<Node3D>(LeftHandAttachPath);
	}

	public void Init(ToolItem defaultTool)
	{
		_defaultTool = defaultTool;
	}

	public Item HeldItem => _heldItem;

	public ToolItem ActiveTool => _heldItem as ToolItem ?? _defaultTool;

	public ToolItem BlockingTool
	{
		get
		{
			if (_offhandTool is { BlockStats.CanBlock: true })
				return _offhandTool;

			var activeTool = ActiveTool;
			return activeTool is { BlockStats.CanBlock: true } ? activeTool : null;
		}
	}

	public string OffhandToolId => _offhandTool?.Id ?? string.Empty;

	public bool UpdateHeldItem(Item item)
	{
		item ??= _defaultTool;

		if (item == _lastHeldItem)
			return false;

		_lastHeldItem = item;
		_heldItem = item;
		ApplyHeldItemVisual(item);
		return true;
	}

	public bool TryEquipOffhand(Item item, out ToolItem tool)
	{
		tool = item as ToolItem;
		if (tool is not { CanEquipOffhand: true })
			return false;

		SetOffhandTool(tool);
		return true;
	}

	public bool TrySetOffhandById(string itemId)
	{
		if (!TryResolveOffhandTool(itemId, out var tool))
			return false;

		SetOffhandTool(tool);
		return true;
	}

	private void ApplyHeldItemVisual(Item item)
	{
		if (item is ToolItem { HeldItemScene: not null, CanEquipOffhand: false } tool)
			EquipRightHand(tool.HeldItemScene);
		else
			UnequipRightHand();
	}

	private void SetOffhandTool(ToolItem tool)
	{
		_offhandTool = tool;

		if (tool?.HeldItemScene != null)
			EquipLeftHand(tool.HeldItemScene);
		else
			UnequipLeftHand();
	}

	private static bool TryResolveOffhandTool(string itemId, out ToolItem tool)
	{
		tool = null;

		if (string.IsNullOrEmpty(itemId))
			return true;

		if (ItemRegistry.GetItem(itemId) is not ToolItem { CanEquipOffhand: true } resolvedTool)
			return false;

		tool = resolvedTool;
		return true;
	}

	private void EquipRightHand(PackedScene toolScene)
	{
		UnequipRightHand();

		if (toolScene == null) return;

		_currentRightHandTool = toolScene.Instantiate<Node3D>();
		_rightHandAttach.AddChild(_currentRightHandTool);
	}

	private void EquipLeftHand(PackedScene scene)
	{
		UnequipLeftHand();

		if (scene == null) return;

		_currentLeftHandTool = scene.Instantiate<Node3D>();
		_leftHandAttach.AddChild(_currentLeftHandTool);
	}

	private void UnequipRightHand()
	{
		if (_currentRightHandTool == null) return;
		_currentRightHandTool.QueueFree();
		_currentRightHandTool = null;
	}

	private void UnequipLeftHand()
	{
		if (_currentLeftHandTool == null) return;
		_currentLeftHandTool.QueueFree();
		_currentLeftHandTool = null;
	}
}
