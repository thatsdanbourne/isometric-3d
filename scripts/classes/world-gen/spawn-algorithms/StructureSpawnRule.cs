using Godot;
using System.Collections.Generic;

public class StructureSpawnRule
{
	public string Id { get; set; }
	public float Density { get; set; } = 0.05f;
	public int RegionSize = 64;

	public List<StructureVariant> Variants { get; set; } = new();
}

public class StructureVariant
{
	public string Id { get; set; }
	public float Weight { get; set; } = 1f;
}

public class StructureDefinition
{
	public string Id { get; set; }
	public int StableId { get; set; }
	public Vector2I Size { get; set; }
	public List<StructureObject> Objects { get; set; } = new();
}

public class StructureObject
{
	public string ObjectId { get; set; }
	public Vector2I LocalTile { get; set; }
}