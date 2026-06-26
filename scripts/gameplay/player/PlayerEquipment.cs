using Godot;

public partial class PlayerEquipment : Node
{
	[Export] public NodePath RightHandAttachPath;
	[Export] public NodePath LeftHandAttachPath;

	private Node3D _rightHandAttach;
	private Node3D _currentRightHandTool;
	private Node3D _leftHandAttach;
	private Node3D _currentLeftHandTool;

	public override void _Ready()
	{
		_rightHandAttach = GetNode<Node3D>(RightHandAttachPath);
		_leftHandAttach = GetNode<Node3D>(LeftHandAttachPath);
	}

	public void EquipTool(PackedScene toolScene)
	{
		if (_currentRightHandTool != null)
		{
			_currentRightHandTool.QueueFree();
			_currentRightHandTool = null;
		}

		if (toolScene == null) return;

		_currentRightHandTool = toolScene.Instantiate<Node3D>();
		_rightHandAttach.AddChild(_currentRightHandTool);
	}

	public void EquipOffhand(PackedScene scene)
	{
		UnequipOffhand();

		if (scene == null) return;

		_currentLeftHandTool = scene.Instantiate<Node3D>();
		_leftHandAttach.AddChild(_currentLeftHandTool);
	}

	public void UnequipTool()
	{
		if (_currentRightHandTool == null) return;
		_currentRightHandTool.QueueFree();
		_currentRightHandTool = null;
	}

	public void UnequipOffhand()
	{
		if (_currentLeftHandTool == null) return;
		_currentLeftHandTool.QueueFree();
		_currentLeftHandTool = null;
	}
}
