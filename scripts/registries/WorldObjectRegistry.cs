using System.Collections.Generic;
using Godot;

public static class WorldObjectRegistry
{
	private static Dictionary<string, WorldObjectDefinition> _defs = new();

	public static void Register(string id, PackedScene scene, float MaxHealth = 10, ToolTier ToolTier = ToolTier.Fist,
		bool blocksTile = true, bool canBeBroken = true, bool isDecor = false, bool is3D = false
	)
	{
		_defs[id] = new WorldObjectDefinition
		{
			Id = id,
			Scene = scene,
			MaxHealth = MaxHealth,
			ToolTier = ToolTier,
			BlocksTile = blocksTile,
			CanBeBroken = canBeBroken,
			IsDecor = isDecor,
			Is3D = is3D
		};
	}

	public static PackedScene GetScene(string id)
	{
		if (_defs.TryGetValue(id, out var def))
			return def.Scene;

		GD.PrintErr($"WorldObjectRegistry: No definition found for id '{id}'");
		return null;
	}

	public static WorldObjectDefinition GetDefinition(string id)
	{
		if (_defs.TryGetValue(id, out var def))
			return def;

		GD.PrintErr($"WorldObjectRegistry: No definition found for id '{id}'");
		return null;
	}

	public static void RegisterDefaults()
	{
		Register("tree_oak", GD.Load<PackedScene>("res://scenes/terrain/objects/TreeOak.tscn"), is3D: true);
		Register("tree_birch", GD.Load<PackedScene>("res://scenes/terrain/objects/TreeBirch.tscn"), is3D: true);
		Register("tree_pine", GD.Load<PackedScene>("res://scenes/terrain/objects/TreePine.tscn"), is3D: true);
		Register("rock", GD.Load<PackedScene>("res://scenes/terrain/objects/Rock.tscn"), 15f, is3D: true);
		Register("rock_coal", GD.Load<PackedScene>("res://scenes/terrain/objects/RockCoalOre.tscn"), 18f, is3D: true);
		Register("rock_copper", GD.Load<PackedScene>("res://scenes/terrain/objects/RockCopperOre.tscn"), 20f, ToolTier.Stone, is3D: true);
		Register("campfire", GD.Load<PackedScene>("res://scenes/placeables/Campfire.tscn"));
		Register("crafting_table", GD.Load<PackedScene>("res://scenes/placeables/CraftingTable.tscn"));
		Register("kiln", GD.Load<PackedScene>("res://scenes/placeables/Kiln.tscn"));
		Register("chest", GD.Load<PackedScene>("res://scenes/placeables/ChestOne.tscn"), is3D: true);
	}
}

public class WorldObjectDefinition
{
	public string Id;
	public PackedScene Scene;
	public ToolTier ToolTier;
	public float MaxHealth;
	public bool BlocksTile = true;
	public bool CanBeBroken = true;
	public bool IsDecor = false;
	public bool Is3D = false;
}
