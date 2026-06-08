using System.Collections.Generic;
using Godot;

public static class WorldObjectRegistry
{
	private static readonly Dictionary<int, WorldObjectDefinition> Defs = new();

	private static void Register(
		string id,
		PackedScene scene,
		float maxHealth = 10,
		ToolTier toolTier = ToolTier.Fist,
		bool blocksTile = true
	)
	{
		var uid = DeterministicHash.String32(id);

		Defs[uid] = new WorldObjectDefinition
		{
			Id = id,
			StableId = uid,
			Scene = scene,
			MaxHealth = maxHealth,
			ToolTier = toolTier,
			BlocksTile = blocksTile
		};
	}

	public static PackedScene GetScene(int uid)
	{
		if (Defs.TryGetValue(uid, out var def))
			return def.Scene;

		GD.PrintErr($"WorldObjectRegistry: No definition found for uid '{uid}'");
		return null;
	}

	public static WorldObjectDefinition GetDefinition(int uid)
	{
		if (Defs.TryGetValue(uid, out var def))
			return def;

		GD.PrintErr($"WorldObjectRegistry: No definition found for uid '{uid}'");
		return null;
	}

	public static WorldObjectDefinition GetDefinition(string id)
	{
		if (Defs.TryGetValue(DeterministicHash.String32(id), out var def))
			return def;

		GD.PrintErr($"WorldObjectRegistry: No definition found for id '{id}'");
		return null;
	}

	public static void RegisterDefaults()
	{
		Register("tree_oak", GD.Load<PackedScene>("res://scenes/terrain/objects/TreeOak.tscn"));
		Register("tree_birch", GD.Load<PackedScene>("res://scenes/terrain/objects/TreeBirch.tscn"));
		Register("tree_pine", GD.Load<PackedScene>("res://scenes/terrain/objects/TreePine.tscn"));
		Register("stone", GD.Load<PackedScene>("res://scenes/terrain/objects/Stone.tscn"));
		Register("stick_pile", GD.Load<PackedScene>("res://scenes/terrain/objects/StickPile.tscn"));
		Register("rock", GD.Load<PackedScene>("res://scenes/terrain/objects/Rock.tscn"), 15f);
		Register("rock_coal", GD.Load<PackedScene>("res://scenes/terrain/objects/RockCoalOre.tscn"), 18f);
		Register("rock_copper", GD.Load<PackedScene>("res://scenes/terrain/objects/RockCopperOre.tscn"), 20f,
			ToolTier.Stone);
		Register("campfire", GD.Load<PackedScene>("res://scenes/placeables/Campfire.tscn"));
		Register("crafting_table", GD.Load<PackedScene>("res://scenes/placeables/CraftingTable.tscn"));
		Register("kiln", GD.Load<PackedScene>("res://scenes/placeables/Kiln.tscn"));
		Register("chest", GD.Load<PackedScene>("res://scenes/placeables/ChestOne.tscn"));
		Register("flower_poppy", GD.Load<PackedScene>("res://scenes/terrain/decor/FlowerPoppy.tscn"));
		Register("reeds", GD.Load<PackedScene>("res://scenes/terrain/objects/Reeds.tscn"));
		Register("tall_grass", GD.Load<PackedScene>("res://scenes/terrain/decor/TallGrass.tscn"));
		Register("standing_stone", GD.Load<PackedScene>("res://scenes/structures/StandingStone.tscn"));
	}
}

public class WorldObjectDefinition
{
	public string Id;
	public int StableId;
	public PackedScene Scene;
	public ToolTier ToolTier;
	public float MaxHealth;
	public bool BlocksTile;
}