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

		ItemRegistry.RegisterItem(new ToolItem
		{
			Id = "stone_sword",
			ToolType = "sword",
			DisplayName = "Stone Sword",
			Description = "A decent starter sword.",
			Icon = LoadTexture("stone_sword"),
			StackSize = 1,
			Damage = 1.25f,
			HitArcDegrees = 85f,
			HitRayCount = 5,
			HitRange = 1.35f,
			CooldownSeconds = 0.45f,
			DamageMultipliers = new Dictionary<string, float>
			{
				{ "stone", 1.5f },
				{ "ore", 1.5f },
				{ "wood", 1f }
			},
			HeldItemScene = GD.Load<PackedScene>("res://scenes/tools/StoneSword.tscn")
		});

		ItemRegistry.RegisterItem(new ToolItem
		{
			Id = "stone_pickaxe",
			ToolType = "pickaxe",
			DisplayName = "Stone Pickaxe",
			Description = "Now go find some iron!",
			Icon = LoadTexture("stone_pickaxe"),
			StackSize = 1,
			Damage = 1.5f,
			DamageMultipliers = new Dictionary<string, float>
			{
				{ "stone", 2.0f },
				{ "ore", 1.5f },
				{ "wood", 0.25f }
			},
			HitArcDegrees = 35f,
			HitRayCount = 3,
			HitRange = 1.8f,
			Tier = ToolTier.Stone
		});

		ItemRegistry.RegisterItem(new ToolItem
		{
			Id = "stone_axe",
			ToolType = "axe",
			DisplayName = "Stone Axe",
			Description = "Beats punching trees...",
			Icon = LoadTexture("stone_axe"),
			Damage = 1.25f,
			StackSize = 1,
			DamageMultipliers = new Dictionary<string, float>
			{
				{ "stone", 0.25f },
				{ "wood", 2.0f }
			},
			HitArcDegrees = 75f,
			HitRayCount = 5,
			HitRange = 1.5f,
			CooldownSeconds = 0.6f,
			Tier = ToolTier.Stone,
			HeldItemScene = GD.Load<PackedScene>("res://scenes/tools/Axe.tscn")
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
			DamageMultipliers = new Dictionary<string, float>
			{
				{ "stone", 2.5f },
				{ "ore", 2.0f },
				{ "wood", 0.5f }
			},
			HitArcDegrees = 35f,
			HitRayCount = 3,
			HitRange = 2.0f,
			Tier = ToolTier.Copper
		});
	}

	private static Texture2D LoadTexture(string name)
	{
		return GD.Load<Texture2D>($"res://assets/icons/{name}.png");
	}
}