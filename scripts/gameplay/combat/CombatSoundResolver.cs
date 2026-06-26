using Godot;

public static class CombatSoundResolver
{
	public static CombatSoundKeys Resolve(ToolItem tool, IToolHittable target, ToolHitResponse response)
	{
		var hitSound = ResolveHitSound(tool, target);
		var primarySound = response.Outcome switch
		{
			ToolHitOutcome.Blocked => ResolveBlockSound(tool, target, response.BlockingTool, hitSound),
			ToolHitOutcome.Failed => ResolveFailedSound(target),
			_ => hitSound
		};

		return new CombatSoundKeys(
			primarySound,
			response.Outcome == ToolHitOutcome.Destroyed ? target.GetBreakSound() : string.Empty
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

	private static string ResolveBlockSound(ToolItem attackingTool, IToolHittable target, ToolItem blockingTool,
		string fallbackSound)
	{
		var targetSound = target.GetBlockSound(blockingTool);
		GD.Print($"Block sound: {targetSound}");
		if (!string.IsNullOrEmpty(targetSound))
			return targetSound;

		if (!string.IsNullOrEmpty(blockingTool?.BlockSoundKey))
			return blockingTool.BlockSoundKey;

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