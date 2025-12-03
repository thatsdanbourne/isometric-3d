using Godot;
using System.Collections.Generic;

public class ToolItem : Item
{
    public float Damage { get; set; } = 1.0f;
    public string ToolType { get; set; } = "";
    public string SwingSoundsKey { get; set; } = "fist_1";
    public Dictionary<string, float> DamageMultipliers { get; set; } = new();

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
