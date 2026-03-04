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

	public ToolHitResult UseOn(IToolHittable target, Vector3 fromDirection, Vector3 hitPoint)
	{
		if (target == null) return ToolHitResult.None;

		if (target is WorldObject wo)
			if (wo.RequiredTier > Tier)
			{
				var outcomeFail = target.ReceiveToolHitFailed(this, fromDirection, hitPoint);
				return new ToolHitResult(outcomeFail, target.GetHitRoot(), hitPoint);
			}

		var finalDamage = Damage;
		finalDamage = target.ModifyIncomingToolDamage(this, finalDamage, Damage);
		var outcome = target.ReceiveToolHit(this, finalDamage, fromDirection, hitPoint);
		return new ToolHitResult(outcome, target.GetHitRoot(), hitPoint);
	}
}

public readonly struct ToolHitResult(ToolHitOutcome outcome, Node3D targetRoot, Vector3 hitPoint)
{
	public ToolHitOutcome Outcome { get; } = outcome;
	public Node3D TargetRoot { get; } = targetRoot;
	public Vector3 HitPoint { get; } = hitPoint;

	public static ToolHitResult None => new(ToolHitOutcome.None, null, Vector3.Zero);
}