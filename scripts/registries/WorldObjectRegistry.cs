using System.Collections.Generic;
using Godot;

public static class WorldObjectRegistry
{
	private static Dictionary<string, PackedScene> _scenes = new();

	public static void Register(string id, PackedScene scene)
	{
		_scenes[id] = scene;
	}

	public static PackedScene GetScene(string id)
	{
		if (_scenes.TryGetValue(id, out var scene))
			return scene;

		GD.PrintErr($"WorldObjectRegistry: No scene found for id '{id}'");
		return null;
	}

	public static void RegisterDefaults()
	{
		Register("tree_pine", GD.Load<PackedScene>("res://scenes/terrain/objects/TreePine.tscn"));
		Register("tree_birch", GD.Load<PackedScene>("res://scenes/terrain/objects/TreeBirch.tscn"));
		Register("rock", GD.Load<PackedScene>("res://scenes/terrain/objects/Rock.tscn"));
		Register("rock_coal", GD.Load<PackedScene>("res://scenes/terrain/objects/RockCoalOre.tscn"));
		Register("flower_poppy", GD.Load<PackedScene>("res://scenes/terrain/decor/FlowerPoppy.tscn"));
	}
}
