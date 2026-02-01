using Godot;
using System.Collections.Generic;

public partial class PlacementPreview : Node3D
{
	private Node3D previewInstance;
	private readonly List<StandardMaterial3D> previewMaterials = new();

	public void SetPreviewScene(PackedScene scene)
	{
		ClearPreview();
		previewInstance = scene.Instantiate<Node3D>();
		AddChild(previewInstance);
		previewInstance.RotationDegrees = new Vector3(0f, -45f, 0f);

		ApplyPreviewMaterial(previewInstance);
	}

	private void ClearPreview()
	{
		if (previewInstance != null)
		{
			previewInstance.QueueFree();
			previewInstance = null;
		}

		previewMaterials.Clear();
	}

	public void SetValid(bool valid)
	{
		var color = valid
			? new Color(0.3f, 1f, 0.3f, 0.6f)
			: new Color(1f, 0.3f, 0.3f, 0.6f);

		foreach (var mat in previewMaterials)
			mat.AlbedoColor = color;
	}

	private void ApplyPreviewMaterial(Node node)
	{
		if (node is MeshInstance3D mesh)
		{
			mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;

			var mat = new StandardMaterial3D
			{
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				AlbedoColor = new Color(0.3f, 1f, 0.3f, 0.6f),
				CullMode = BaseMaterial3D.CullModeEnum.Disabled
			};

			mesh.MaterialOverride = mat;
			previewMaterials.Add(mat);
		}

		foreach (var child in node.GetChildren())
			ApplyPreviewMaterial(child);
	}
}
