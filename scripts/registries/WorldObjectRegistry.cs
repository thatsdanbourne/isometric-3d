using System.Collections.Generic;
using Godot;

public static class WorldObjectRegistry
{
	private static readonly Dictionary<int, WorldObjectDefinition> Defs = new();

	private static void Register(string id, PackedScene scene, float maxHealth = 10, ToolTier toolTier = ToolTier.Fist,
		bool blocksTile = true
	)
	{
		var typeId = StableHash(id);
		Defs[typeId] = new WorldObjectDefinition
		{
			Id = id,
			TypeId = typeId,
			Scene = scene,
			MaxHealth = maxHealth,
			ToolTier = toolTier,
			BlocksTile = blocksTile
		};
	}

	public static PackedScene GetScene(int typeId)
	{
		if (Defs.TryGetValue(typeId, out var def))
			return def.Scene;

		GD.PrintErr($"WorldObjectRegistry: No definition found for typeId '{typeId}'");
		return null;
	}

	public static WorldObjectDefinition GetDefinition(int typeId)
	{
		if (Defs.TryGetValue(typeId, out var def))
			return def;

		GD.PrintErr($"WorldObjectRegistry: No definition found for typeId '{typeId}'");
		return null;
	}

	public static void RegisterDefaults()
	{
		Register("tree_oak", GD.Load<PackedScene>("res://scenes/terrain/objects/TreeOak.tscn"));
		Register("tree_birch", GD.Load<PackedScene>("res://scenes/terrain/objects/TreeBirch.tscn"));
		Register("tree_pine", GD.Load<PackedScene>("res://scenes/terrain/objects/TreePine.tscn"));
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
	}

	public static int StableHash(string s)
	{
		unchecked
		{
			const int fnvPrime = 16777619;
			var hash = (int)2166136261;

			foreach (var t in s)
			{
				hash ^= t;
				hash *= fnvPrime;
			}

			return hash;
		}
	}
}

public class WorldObjectDefinition
{
	public string Id;
	public int TypeId;
	public PackedScene Scene;
	public ToolTier ToolTier;
	public float MaxHealth;
	public bool BlocksTile;
}