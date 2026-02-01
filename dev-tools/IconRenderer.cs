using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

[Tool]
public partial class IconRenderer : Node3D
{
	[Export] public SubViewport Viewport;
	[Export] public Node3D PreviewRoot;
	[Export] public Camera3D Camera;
	[Export] public DirectionalLight3D Light;

	[Export] public Vector2I IconSize = new Vector2I(128, 128);
	[Export] public float PaddingMultiplier = 1.2f;

	private bool isGenerating = false;

	[Export]
	public bool GenerateTestIcon
	{
		get => false;
		set
		{
			if (!Engine.IsEditorHint() || !value || isGenerating)
				return;

			_ = GenerateTestIconAsync();
		}
	}

	[Export]
	public bool ExportIcon
	{
		get => false;
		set
		{
			if (!Engine.IsEditorHint() || !value || isGenerating)
				return;

			Image img = Viewport.GetTexture().GetImage();
			img.SavePng("res://assets/icons/campfire.png");
			GD.Print("Icon exported: res://assets/icons/campfire.png");
		}
	}

	private async Task GenerateTestIconAsync()
	{
		isGenerating = true;

		await GenerateIcon(
			GD.Load<PackedScene>("res://assets/meshes/kiln/Kiln.glb"),
			"res://assets/icons/kiln.png"
		);

		isGenerating = false;

		// Reset the checkbox cleanly
		NotifyPropertyListChanged();
	}

	public override void _Ready()
	{
		if (!Engine.IsEditorHint())
			return;

		Viewport.TransparentBg = true;
		Viewport.Size = IconSize;
	}

	// === Public API ===

	public async Task GenerateIcon(PackedScene sourceScene, string outputPath)
	{
		ClearPreview();

		// Instantiate preview
		Node3D instance = sourceScene.Instantiate<Node3D>();
		instance.Visible = true;
		PreviewRoot.Visible = true;
		PreviewRoot.AddChild(instance);

		// Aabb bounds = CalculateCombinedAabb(instance);

		// if (bounds.Size == Vector3.Zero)
		// {
		// 	GD.PushError("IconRenderer: Scene has no meshes");
		// 	ClearPreview();
		// 	return;
		// }

		// // Center object
		// PreviewRoot.Position = -bounds.GetCenter();
		// PreviewRoot.Position += Vector3.Down * bounds.Size.Y * 0.1f;

		// // Fit camera
		// float maxExtent = Mathf.Max(bounds.Size.X, bounds.Size.Z);
		// Camera.Size = maxExtent * PaddingMultiplier;

		// Camera.Current = true;

		// 🔥 THIS IS CRITICAL
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		Image img = Viewport.GetTexture().GetImage();
		img.SavePng(outputPath);

		GD.Print($"Icon saved: {outputPath}");

		ClearPreview();
	}

	// === Internals ===

	private void ClearPreview()
	{
		foreach (Node child in PreviewRoot.GetChildren())
			child.QueueFree();
	}

	private Aabb CalculateCombinedAabb(Node root)
	{
		bool hasBounds = false;
		Aabb combined = new Aabb();

		foreach (Node node in GetAllChildren(root))
		{
			if (node is MeshInstance3D mesh && mesh.Mesh != null)
			{
				Aabb aabb = mesh.Mesh.GetAabb();
				aabb = aabb * mesh.GlobalTransform;

				if (!hasBounds)
				{
					combined = aabb;
					hasBounds = true;
				}
				else
				{
					combined = combined.Merge(aabb);
				}
			}
		}

		return combined;
	}

	private IEnumerable<Node> GetAllChildren(Node root)
	{
		foreach (Node child in root.GetChildren())
		{
			yield return child;

			foreach (Node sub in GetAllChildren(child))
				yield return sub;
		}
	}
}