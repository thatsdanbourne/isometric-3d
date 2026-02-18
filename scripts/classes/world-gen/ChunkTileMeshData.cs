using Godot;
using System.Collections.Generic;

public partial class ChunkTileMeshData : RefCounted
{
	public string MeshId;
	public Mesh Mesh;
	public Material Material;
	public List<Transform3D> Transforms = new();
	public List<Color> Colors = new();
}