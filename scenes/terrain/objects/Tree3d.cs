using Godot;
using System;

public partial class Tree3d : Node3D
{
	[Export] public int LeafCount = 120;
	[Export] public float LeafScaleMin = 0.8f;
	[Export] public float LeafScaleMax = 1.2f;

	[Export] public Mesh LeafMesh; // PlaneMesh with leaf texture

	private MeshInstance3D _treeMesh;
	private MultiMeshInstance3D _leavesMMI;

	public override void _Ready()
	{
		_treeMesh = GetNode<MeshInstance3D>("TreeMesh");
		_leavesMMI = GetNode<MultiMeshInstance3D>("Leaves");

		if (_treeMesh.Mesh == null || LeafMesh == null)
		{
			GD.PushError("Tree3d: Missing TreeMesh or LeafMesh.");
			return;
		}

		BuildLeaves();
	}

	private void BuildLeaves()
	{
		Mesh mesh = _treeMesh.Mesh;

		if (mesh.GetSurfaceCount() < 2)
		{
			GD.PushError("Tree3d: Expected canopy emitter as surface 1.");
			return;
		}

		var arrays = mesh.SurfaceGetArrays(1);
		var vertices = (Vector3[])arrays[(int)Mesh.ArrayType.Vertex];
		var indices = (int[])arrays[(int)Mesh.ArrayType.Index];

		var multimesh = new MultiMesh();
		multimesh.TransformFormat = MultiMesh.TransformFormatEnum.Transform3D;
		multimesh.Mesh = LeafMesh;
		multimesh.InstanceCount = LeafCount;

		var rng = new RandomNumberGenerator();
		rng.Seed = GD.Randi();

		for (int i = 0; i < LeafCount; i++)
		{
			// Pick random triangle
			int tri = rng.RandiRange(0, (indices.Length / 3) - 1) * 3;

			Vector3 a = vertices[indices[tri]];
			Vector3 b = vertices[indices[tri + 1]];
			Vector3 c = vertices[indices[tri + 2]];

			// Uniform barycentric sampling
			float r1 = Mathf.Sqrt(rng.Randf());
			float r2 = rng.Randf();

			Vector3 position =
				a * (1 - r1) +
				b * (r1 * (1 - r2)) +
				c * (r1 * r2);

			// Triangle normal
			Vector3 normal = (b - a).Cross(c - a).Normalized();

			// Orient quad to face outward
			Basis basis = Basis.LookingAt(normal, Vector3.Up);

			// Random twist around normal
			basis = basis.Rotated(normal, rng.RandfRange(0, Mathf.Tau));

			// Random scale
			float scale = rng.RandfRange(LeafScaleMin, LeafScaleMax);
			basis = basis.Scaled(Vector3.One * scale);

			Transform3D transform = new Transform3D(basis, position);
			multimesh.SetInstanceTransform(i, transform);
		}

		_leavesMMI.Multimesh = multimesh;
	}

	private void HideCanopySurface()
	{
		// Prevent canopy emitter from rendering
		_treeMesh.SetSurfaceOverrideMaterial(1, null);
	}
}
