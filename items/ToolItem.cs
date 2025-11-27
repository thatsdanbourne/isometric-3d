using Godot;

[GlobalClass]
public partial class ToolItem : Item
{
    [Export] public float Damage { get; set; } = 1.0f;
    [Export] public string ToolType { get; set; } = "";
    [Export] public Godot.Collections.Dictionary<string, float> DamageMultipliers { get; set; } = new Godot.Collections.Dictionary<string, float>();
    [Export] public Godot.Collections.Array<AudioStream> SwingSounds {get; set; } = new Godot.Collections.Array<AudioStream>();

    public void UseOn(Node3D target, Vector3 fromDirection)
    {
        if (target is WorldObject wo)
        {
            float finalDamage = Damage;

            if(DamageMultipliers.TryGetValue(wo.ObjectType, out float multiplier))
                finalDamage *= multiplier;

            
            wo.ApplyDamage(finalDamage, fromDirection);
        }
    }
}
