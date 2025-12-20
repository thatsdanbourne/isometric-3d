using Godot;
using System.Collections.Generic;

public class ToolItem : Item
{
    public float Damage { get; set; } = 1.0f;
    public string ToolType { get; set; } = "";
    public string SwingSoundsKey { get; set; } = "fist_1";
    public Dictionary<string, float> DamageMultipliers { get; set; } = new();
    public float HitArcDegress { get; set; } = 70f;
    public int HitRayCount { get; set; } = 5;
    public float HitRange { get; set; } = 2.0f;
    public ToolTier Tier { get; set; }

    public void UseOn(Node3D target, Vector3 fromDirection)
    {
        if (target is WorldObject wo)
        {
            if (wo.RequiredTier > Tier)
            {
                GD.PrintErr("ToolItem.UseOn: Tool tier too low to damage this object");
                return;
            }

            float finalDamage = Damage;

            if (DamageMultipliers.TryGetValue(wo.ObjectType, out float multiplier))
                finalDamage *= multiplier;


            wo.ApplyDamage(finalDamage, fromDirection);
        }
    }
}

public enum ToolTier
{
    Fist,
    Stone,
    Copper,
    Iron,
    Steel
}