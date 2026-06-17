using Godot;
using System.Collections.Generic;

public static class ToolItems
{
	public static void Register()
	{
		ItemRegistry.RegisterItem(new ToolItem
		{
			Id = "fist",
			DisplayName = "",
			Description = "",
			Damage = 1.0f,
			HitArcDegrees = 90f,
			HitRayCount = 7,
			HitRange = 1.1f,
			Tier = ToolTier.Fist
		});

		#region Swords

		ItemRegistry.RegisterItem(new ToolItem
		{
			Id = "stone_sword",
			ToolType = "sword",
			DisplayName = "Stone Sword",
			Description = "A decent starter sword.",
			Icon = LoadTexture("stone_sword"),
			StackSize = 1,
			Damage = 1.25f,
			Knockback = 4f,
			ChargedDamageMultiplier = 1.75f,
			ChargedKnockbackMultiplier = 1.5f,
			ChargedStaggerMultiplier = 2f,
			ChargedLungeDistance = 0.8f,
			ChargedLungeDuration = 0.12f,
			HitArcDegrees = 85f,
			HitRayCount = 5,
			HitRange = 1.35f,
			CooldownSeconds = 0.6f,
			ComboChainSeconds = 0.5f,
			HeldItemScene = GD.Load<PackedScene>("res://scenes/tools/StoneSword.tscn"),
			SoundSet = "sword",
			DamageMultipliers = new Dictionary<string, float>
			{
				{ "stone", 1.5f },
				{ "wood", 1f }
			}
		});

		#endregion

		#region Pickaxes

		ItemRegistry.RegisterItem(new ToolItem
		{
			Id = "stone_pickaxe",
			ToolType = "pickaxe",
			DisplayName = "Stone Pickaxe",
			Description = "Now go find some iron!",
			Icon = LoadTexture("stone_pickaxe"),
			StackSize = 1,
			Damage = 1.5f,
			HitArcDegrees = 35f,
			HitRayCount = 3,
			HitRange = 1.8f,
			CooldownSeconds = 0.6f,
			Tier = ToolTier.Stone,
			SoundSet = "sword",
			HeldItemScene = GD.Load<PackedScene>("res://scenes/tools/StonePickaxe.tscn"),
			DamageMultipliers = new Dictionary<string, float>
			{
				{ "stone", 2.0f },
				{ "ore", 1.5f },
				{ "wood", 0.25f }
			}
		});

		ItemRegistry.RegisterItem(new ToolItem
		{
			Id = "copper_pickaxe",
			ToolType = "pickaxe",
			DisplayName = "Copper Pickaxe",
			Description = "A decent starter pickaxe.",
			Icon = LoadTexture("copper_pickaxe"),
			StackSize = 1,
			Damage = 1.5f,
			HitArcDegrees = 35f,
			HitRayCount = 3,
			HitRange = 2.0f,
			CooldownSeconds = 0.6f,
			Tier = ToolTier.Copper,
			SoundSet = "sword",
			DamageMultipliers = new Dictionary<string, float>
			{
				{ "stone", 2.5f },
				{ "ore", 2.0f },
				{ "wood", 0.5f }
			}
		});

		#endregion

		#region Axes

		ItemRegistry.RegisterItem(new ToolItem
		{
			Id = "stone_axe",
			ToolType = "axe",
			DisplayName = "Stone Axe",
			Description = "Beats punching trees...",
			Icon = LoadTexture("stone_axe"),
			Damage = 1.25f,
			Knockback = 5f,
			ChargedDamageMultiplier = 2f,
			ChargedKnockbackMultiplier = 2.5f,
			ChargedStaggerMultiplier = 2.5f,
			StackSize = 1,
			HitArcDegrees = 75f,
			HitRayCount = 5,
			HitRange = 1.5f,
			CooldownSeconds = 0.6f,
			Tier = ToolTier.Stone,
			SoundSet = "sword",
			HeldItemScene = GD.Load<PackedScene>("res://scenes/tools/Axe.tscn"),
			DamageMultipliers = new Dictionary<string, float>
			{
				{ "stone", 0.25f },
				{ "wood", 2.0f }
			}
		});

		#endregion
	}

	private static Texture2D LoadTexture(string name)
	{
		return GD.Load<Texture2D>($"res://assets/icons/{name}.png");
	}
}