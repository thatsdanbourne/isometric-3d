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

	[Export] public Vector2I IconSize = new(128, 128);
	[Export] public float PaddingMultiplier = 1.2f;
	[Export] public string OutputFile = "icon.png";

	private bool _isGenerating;


	[Export]
	public bool ExportIcon
	{
		get => false;
		set
		{
			if (!Engine.IsEditorHint() || !value || _isGenerating)
				return;

			var img = Viewport.GetTexture().GetImage();
			img.SavePng("res://assets/icons/" + OutputFile + ".png");
			GD.Print("Icon exported: res://assets/icons/" + OutputFile + ".png");
		}
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
		var instance = sourceScene.Instantiate<Node3D>();
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

		var img = Viewport.GetTexture().GetImage();
		img.SavePng(outputPath);

		GD.Print($"Icon saved: {outputPath}");

		ClearPreview();
	}

	// === Internals ===

	private void ClearPreview()
	{
		foreach (var child in PreviewRoot.GetChildren())
			child.QueueFree();
	}

	private Aabb CalculateCombinedAabb(Node root)
	{
		var hasBounds = false;
		var combined = new Aabb();

		foreach (var node in GetAllChildren(root))
			if (node is MeshInstance3D mesh && mesh.Mesh != null)
			{
				var aabb = mesh.Mesh.GetAabb();
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

		return combined;
	}

	private IEnumerable<Node> GetAllChildren(Node root)
	{
		foreach (var child in root.GetChildren())
		{
			yield return child;

			foreach (var sub in GetAllChildren(child))
				yield return sub;
		}
	}
}