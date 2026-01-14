using Godot;

public abstract partial class StationObject : WorldObject, IInteractable
{
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

    public virtual T GetCapability<T>() where T : class
    {
        return this as T;
    }

    public virtual void Interact(Player player)
    {
        if (this is ICraftingStation station)
        {
            player.OpenCraftingUI(station);
        }
    }

    public virtual void OnFocusGained()
    {
        SetHighlighted(true);
    }

    public virtual void OnFocusLost()
    {
        SetHighlighted(false);
    }

    public void SetHighlighted(bool highlighted)
    {
        outlineMaterial.SetShaderParameter("enabled", highlighted);
    }
}
