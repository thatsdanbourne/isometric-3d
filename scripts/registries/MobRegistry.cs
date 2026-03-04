using Godot;
using System.Collections.Generic;

public class MobRegistry
{
	public static MobRegistry Instance { get; set; } = new();

	private readonly Dictionary<string, PackedScene> _scenes = new();

	public void Register(string id, string scenePath)
	{
		_scenes[id] = GD.Load<PackedScene>(scenePath);
	}

	public PackedScene GetScene(string id)
	{
		if (!_scenes.TryGetValue(id, out var scene))
		{
			GD.PrintErr($"MobRegistry: missing id '{id}'");
			return null;
		}

		return scene;
	}
}