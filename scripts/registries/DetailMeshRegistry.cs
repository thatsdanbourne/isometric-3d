using System.Collections.Generic;
using Godot;

public static class DetailMeshRegistry
{
	private static readonly Dictionary<string, Mesh> Meshes = new();

	public static void Register(string id, string scenePath)
	{
		var packed = GD.Load<PackedScene>(scenePath);
		var root = packed.Instantiate<Node3D>();

		var meshInstance = FindMeshInstance(root);
		if (meshInstance == null)
		{
			GD.PushError($"No MeshInstance3D found in detail mesh scene: {scenePath}");
			root.QueueFree();
			return;
		}

		meshInstance.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
		Meshes[id] = meshInstance.Mesh;
		root.QueueFree();
	}

	public static Mesh Get(string id)
	{
		return Meshes.GetValueOrDefault(id);
	}

	private static MeshInstance3D FindMeshInstance(Node node)
	{
		if (node is MeshInstance3D meshInstance)
			return meshInstance;

		foreach (var child in node.GetChildren())
		{
			var result = FindMeshInstance(child);
			if (result != null)
				return result;
		}

		return null;
	}
}