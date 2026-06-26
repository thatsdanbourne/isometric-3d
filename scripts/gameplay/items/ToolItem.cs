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
	public float Stagger { get; init; } = 1.0f;
	public float CooldownSeconds { get; init; } = 0.55f;
	public float ComboChainSeconds { get; init; } = 0.4f;
	public float ChargedDamageMultiplier { get; set; } = 1f;
	public float ChargedKnockbackMultiplier { get; set; } = 1f;
	public float ChargedStaggerMultiplier { get; set; } = 1f;
	public float ChargedLungeDistance { get; set; } = 0f;
	public float ChargedLungeDuration { get; set; } = 0.12f;
	public BlockStats BlockStats { get; set; } = BlockStats.Default;
	public string BlockSoundKey { get; set; } = "";
	public ToolTier Tier { get; init; }
	public PackedScene HeldItemScene { get; init; }
	public bool CanEquipOffhand { get; init; } = false;

	public ToolHitResult UseOn(IToolHittable target, Vector3 fromDirection, Vector3 hitPoint, AttackContext context)
	{
		if (target == null)
			return ToolHitResult.None;

		var hitRoot = target.GetHitRoot();

		if (target is WorldObject wo)
		{
			if (!wo.CanReceiveToolHits) return ToolHitResult.None;

			if (wo.RequiredTier > Tier)
			{
				var failedHit = target.ReceiveToolHitFailed(this, fromDirection, hitPoint);
				return BuildHitResult(this, target, hitRoot, failedHit, fromDirection, hitPoint);
			}
		}

		var finalDamage = Damage;
		finalDamage = target.ModifyIncomingToolDamage(this, finalDamage, Damage) * context.DamageMultiplier;
		var finalKnockback = Knockback * context.KnockbackMultiplier;
		var finalStagger = Stagger * context.StaggerMultiplier;
		var response = target.ReceiveToolHit(this, finalDamage, finalKnockback, finalStagger, fromDirection, hitPoint);
		return BuildHitResult(this, target, hitRoot, response, fromDirection, hitPoint);
	}

	private static ToolHitResult BuildHitResult(ToolItem tool, IToolHittable target, Node3D hitRoot,
		ToolHitResponse response, Vector3 fromDirection, Vector3 hitPoint)
	{
		var sounds = CombatSoundResolver.Resolve(tool, target, response);
		var result = new ToolHitResult(response.Outcome, target.GetImpactType(), sounds.PrimarySoundKey,
			sounds.BreakSoundKey, hitPoint);

		if (hitRoot is Player player)
			result = result.WithPlayerTarget(player.PlayerId, player.Health, fromDirection, response.Knockback);

		return result;
	}
}

public readonly struct ToolHitResponse(ToolHitOutcome outcome, float knockback = 0f, ToolItem blockingTool = null)
{
	public ToolHitOutcome Outcome { get; } = outcome;
	public float Knockback { get; } = knockback;
	public ToolItem BlockingTool { get; } = blockingTool;

	public static ToolHitResponse Hit(float knockback = 0f)
	{
		return new ToolHitResponse(ToolHitOutcome.Hit, knockback);
	}

	public static ToolHitResponse Blocked(float knockback = 0f, ToolItem blockingTool = null)
	{
		return new ToolHitResponse(ToolHitOutcome.Blocked, knockback, blockingTool);
	}

	public static ToolHitResponse Destroyed(float knockback = 0f)
	{
		return new ToolHitResponse(ToolHitOutcome.Destroyed, knockback);
	}

	public static ToolHitResponse Failed()
	{
		return new ToolHitResponse(ToolHitOutcome.Failed);
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

public struct BlockStats
{
	public bool CanBlock;
	public float DamageReduction;
	public float KnockbackReduction;
	public float PoiseReduction;
	public float ArcDegrees;

	public static readonly BlockStats Default = new()
	{
		CanBlock = true,
		DamageReduction = 1f,
		KnockbackReduction = 0.3f,
		PoiseReduction = 0.3f,
		ArcDegrees = 120f
	};
}

public readonly struct ToolHitResult(
	ToolHitOutcome outcome,
	string targetType,
	string primarySoundKey,
	string breakSoundKey,
	Vector3 hitPoint,
	int targetPlayerId = -1,
	float targetHealth = 0f,
	Vector3 hitDirection = default,
	float knockback = 0f)
{
	public ToolHitOutcome Outcome { get; } = outcome;
	public string TargetType { get; } = targetType;
	public string PrimarySoundKey { get; } = primarySoundKey;
	public string BreakSoundKey { get; } = breakSoundKey;
	public Vector3 HitPoint { get; } = hitPoint;
	public int TargetPlayerId { get; } = targetPlayerId;
	public float TargetHealth { get; } = targetHealth;
	public Vector3 HitDirection { get; } = hitDirection;
	public float Knockback { get; } = knockback;
	public bool HasPlayerTarget => TargetPlayerId > 0;

	public static ToolHitResult None =>
		new(ToolHitOutcome.None, string.Empty, string.Empty, string.Empty, Vector3.Zero);

	public ToolHitResult WithPlayerTarget(int playerId, float health, Vector3 direction, float appliedKnockback)
	{
		return new ToolHitResult(Outcome, TargetType, PrimarySoundKey, BreakSoundKey, HitPoint, playerId, health,
			direction, appliedKnockback);
	}
}
