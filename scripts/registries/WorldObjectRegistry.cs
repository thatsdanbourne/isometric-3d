using System.Collections.Generic;
using Godot;

public static class WorldObjectRegistry
{
	private static Dictionary<string, WorldObjectDefinition> _defs = new();

	public static void Register(string id, PackedScene scene, ToolTier ToolTier = ToolTier.Fist,
		bool blocksTile = true, bool canBeBroken = true, bool isDecor = false
	)
	{
		_defs[id] = new WorldObjectDefinition
		{
			Id = id,
			Scene = scene,
			ToolTier = ToolTier,
			BlocksTile = blocksTile,
			CanBeBroken = canBeBroken,
			IsDecor = isDecor,
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
		Register("tree_pine", GD.Load<PackedScene>("res://scenes/terrain/objects/TreePine.tscn"));
		Register("tree_birch", GD.Load<PackedScene>("res://scenes/terrain/objects/TreeBirch.tscn"));
		Register("rock", GD.Load<PackedScene>("res://scenes/terrain/objects/Rock.tscn"));
		Register("rock_coal", GD.Load<PackedScene>("res://scenes/terrain/objects/RockCoalOre.tscn"));
		Register("rock_copper", GD.Load<PackedScene>("res://scenes/terrain/objects/RockCopperOre.tscn"), ToolTier.Stone);
		Register("flower_poppy", GD.Load<PackedScene>("res://scenes/terrain/decor/FlowerPoppy.tscn"), blocksTile: false, isDecor: true);
		Register("campfire", GD.Load<PackedScene>("res://scenes/placeables/Campfire.tscn"));
	}
}

public class WorldObjectDefinition
{
	public string Id;
	public PackedScene Scene;
	public ToolTier ToolTier;

	public bool BlocksTile = true;
	public bool CanBeBroken = true;
	public bool IsDecor = false;
}
