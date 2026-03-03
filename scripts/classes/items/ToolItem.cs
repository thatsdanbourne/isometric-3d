using Godot;
using System.Collections.Generic;

public class ToolItem : Item
{
	public float Damage { get; init; } = 1.0f;
	public string ToolType { get; set; } = "";
	public string SwingSoundsKey { get; set; } = "fist_1";
	public Dictionary<string, float> DamageMultipliers { get; init; } = new();
	public float HitArcDegrees { get; init; } = 70f;
	public int HitRayCount { get; init; } = 5;
	public float HitRange { get; init; } = 2.0f;
	public ToolTier Tier { get; init; }
	public PackedScene HeldItemScene { get; init; }

	public void UseOn(Node3D target, Vector3 fromDirection)
	{
		if (target is not WorldObject wo) return;

		if (wo.RequiredTier > Tier)
		{
			wo.HitFailed();
			return;
		}

		var finalDamage = Damage;

		if (DamageMultipliers.TryGetValue(wo.ObjectType, out var multiplier))
			finalDamage *= multiplier;


		wo.ApplyDamage(finalDamage, fromDirection);
	}
}