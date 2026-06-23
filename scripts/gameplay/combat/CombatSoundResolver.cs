public static class CombatSoundResolver
{
	public static CombatSoundKeys Resolve(ToolItem tool, IToolHittable target, ToolHitOutcome outcome)
	{
		var hitSound = ResolveHitSound(tool, target);
		var primarySound = outcome switch
		{
			ToolHitOutcome.Blocked => ResolveBlockSound(tool, target, hitSound),
			ToolHitOutcome.Failed => ResolveFailedSound(target),
			_ => hitSound
		};

		return new CombatSoundKeys(
			primarySound,
			outcome == ToolHitOutcome.Destroyed ? target.GetBreakSound() : string.Empty
		);
	}

	private static string ResolveHitSound(ToolItem tool, IToolHittable target)
	{
		var targetSound = target.GetHitSound(tool);
		if (!string.IsNullOrEmpty(targetSound))
			return targetSound;

		var impactType = target.GetImpactType();
		if (string.IsNullOrEmpty(impactType))
			return string.Empty;

		return tool.ToolType switch
		{
			"sword" or "axe" when impactType == "flesh" => "hit_flesh_blade",
			_ => $"hit_{impactType}"
		};
	}

	private static string ResolveBlockSound(ToolItem tool, IToolHittable target, string fallbackSound)
	{
		var targetSound = target.GetBlockSound(tool);
		if (!string.IsNullOrEmpty(targetSound))
			return targetSound;

		if (!string.IsNullOrEmpty(tool.BlockSoundKey))
			return tool.BlockSoundKey;

		return fallbackSound;
	}

	private static string ResolveFailedSound(IToolHittable target)
	{
		var failedSound = target.GetFailedHitSound();
		return string.IsNullOrEmpty(failedSound) ? "hit_fail" : failedSound;
	}
}

public readonly struct CombatSoundKeys(string primarySoundKey, string breakSoundKey)
{
	public string PrimarySoundKey { get; } = primarySoundKey;
	public string BreakSoundKey { get; } = breakSoundKey;
}