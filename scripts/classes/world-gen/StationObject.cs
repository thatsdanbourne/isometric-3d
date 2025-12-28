using Godot;

public partial class StationObject : WorldObject, IInteractable
{
    [Export] public StationType StationType;
    private ShaderMaterial outlineMaterial;

    public override void _Ready()
    {
        base._Ready();

        if (visual is not Sprite3D sprite)
            return;

        if (sprite.MaterialOverride is not BaseMaterial3D baseMat)
            return;

        outlineMaterial = new ShaderMaterial();
        outlineMaterial.Shader = GD.Load<Shader>("res://resources/shaders/Outline.gdshader");

        outlineMaterial.SetShaderParameter("albedo_texture", sprite.Texture);
        outlineMaterial.SetShaderParameter("enabled", false);

        baseMat.NextPass = outlineMaterial;
    }

    public void OnFocusGained()
    {
        SetHighlighted(true);
    }

    public void OnFocusLost()
    {
        SetHighlighted(false);
    }

    public override void SetHighlighted(bool highlighted)
    {
        outlineMaterial.SetShaderParameter("enabled", highlighted);
    }
}
