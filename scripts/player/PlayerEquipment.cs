using Godot;

public partial class PlayerEquipment : Node
{
	[Export] public NodePath RightHandAttachPath;

	private Node3D _rightHandAttach;
	private Node3D _currentTool;

	public override void _Ready()
	{
		_rightHandAttach = GetNode<Node3D>(RightHandAttachPath);
	}

	public void EquipTool(PackedScene toolScene)
	{
		if (_currentTool != null)
		{
			_currentTool.QueueFree();
			_currentTool = null;
		}

		if (toolScene == null) return;

		_currentTool = toolScene.Instantiate<Node3D>();
		_rightHandAttach.AddChild(_currentTool);
	}

	public void UnequipTool()
	{
		if (_currentTool == null) return;
		_currentTool.QueueFree();
		_currentTool = null;
	}
}