using Godot;
using System.Collections.Generic;

public class ToolItem : Item
{
	public float Damage { get; init; } = 1.0f;
	public string ToolType { get; set; } = "";
	public Dictionary<string, float> DamageMultipliers { get; init; } = new();
	public float HitArcDegrees { get; init; } = 70f;
	public int HitRayCount { get; init; } = 5;
	public float HitRange { get; init; } = 1.0f;
	public float Knockback { get; init; } = 1.0f;
	public float CooldownSeconds { get; init; } = 0.5f;
	public ToolTier Tier { get; init; }
	public PackedScene HeldItemScene { get; init; }

	public ToolHitResult UseOn(IToolHittable target, Vector3 fromDirection, Vector3 hitPoint, AttackContext context)
	{
		if (target == null)
			return ToolHitResult.None;

		var hitSound = target.GetHitSound(this);
		var breakSound = target.GetBreakSound();

		if (target is WorldObject wo)
		{
			if (!wo.CanReceiveToolHits) return ToolHitResult.None;

			if (wo.RequiredTier > Tier)
			{
				var outcomeFail = target.ReceiveToolHitFailed(this, fromDirection, hitPoint);
				return new ToolHitResult(outcomeFail, target.GetImpactType(), hitSound, string.Empty,
					hitPoint);
			}
		}

		var finalDamage = Damage;
		finalDamage = target.ModifyIncomingToolDamage(this, finalDamage, Damage) * context.DamageMultiplier;
		var finalKnockback = Knockback * context.KnockbackMultiplier;
		var outcome = target.ReceiveToolHit(this, finalDamage, finalKnockback, fromDirection, hitPoint);
		return new ToolHitResult(outcome, target.GetImpactType(), hitSound, breakSound,
			hitPoint);
	}
}

public readonly struct AttackContext
{
	public bool IsCharged { get; init; }

	public float DamageMultiplier { get; init; }
	public float KnockbackMultiplier { get; init; }
	public float StaggerMultiplier { get; init; }

	public static AttackContext Default => new()
	{
		DamageMultiplier = 1f,
		KnockbackMultiplier = 1f,
		StaggerMultiplier = 1f
	};
}

public readonly struct ToolHitResult(
	ToolHitOutcome outcome,
	string targetType,
	string hitSoundKey,
	string breakSoundKey,
	Vector3 hitPoint)
{
	public ToolHitOutcome Outcome { get; } = outcome;
	public string TargetType { get; } = targetType;
	public string HitSoundKey { get; } = hitSoundKey;
	public string BreakSoundKey { get; } = breakSoundKey;
	public Vector3 HitPoint { get; } = hitPoint;

	public static ToolHitResult None =>
		new(ToolHitOutcome.None, string.Empty, string.Empty, string.Empty, Vector3.Zero);
}