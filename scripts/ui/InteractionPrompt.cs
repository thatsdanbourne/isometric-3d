using Godot;

public partial class InteractionPrompt : Node3D
{
	private MeshInstance3D _inputIcon;

	public override void _Ready()
	{
		_inputIcon = GetNode<MeshInstance3D>("InputIcon");
	}

	public void ShowIcon()
	{
		_inputIcon.Visible = true;
	}

	public void HideIcon()
	{
		if (IsInstanceValid(_inputIcon))
			_inputIcon.Visible = false;
	}
}