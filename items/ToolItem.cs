using Godot;

[GlobalClass]
public partial class ToolItem : Item
{
    [Export] public float Damage { get; set; } = 1.0f;
    [Export] public string ToolType { get; set; } = "";
    [Export] public Godot.Collections.Array<AudioStream> SwingSounds {get; set; } = new Godot.Collections.Array<AudioStream>();

    public void UseOn(Node3D target)
    {
        if (target is WorldObject wo)
        {
            wo.ApplyDamage(Damage);
        }
    }
}
