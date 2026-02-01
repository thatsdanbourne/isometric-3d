using Godot;

public partial class InteractionPrompt : Node3D
{
    private MeshInstance3D inputIcon;

    public override void _Ready()
    {
        inputIcon = GetNode<MeshInstance3D>("InputIcon");
    }

    public void ShowIcon()
    {
        inputIcon.Visible = true;
    }

    public void HideIcon()
    {
        if (IsInstanceValid(inputIcon))
            inputIcon.Visible = false;
    }
}