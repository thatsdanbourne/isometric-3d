using Godot;

public partial class ShadowCard : Sprite3D
{
	private float _originalHeight;
	private float _originalWidth;


	public override void _Ready()
	{
		Texture2D tex = Texture;
		float px = PixelSize;

		_originalWidth = tex.GetSize().X * px;
		_originalHeight = tex.GetSize().Y * px;

		var mat = new StandardMaterial3D
		{
			AlbedoTexture = tex,
			Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor,
			AlphaScissorThreshold = 0.1f,
			TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			DisableReceiveShadows = true
		};

		CastShadow = ShadowCastingSetting.ShadowsOnly;
		MaterialOverride = mat;
	}

	public void ApplyShadow(in Basis cardBasis, float stretch)
	{
		Transform3D xf = GlobalTransform;
		xf.Basis = cardBasis;
		GlobalTransform = xf;

		Scale = new Vector3(Scale.X, Scale.Y * stretch, Scale.Z);
	}

	public override void _EnterTree()
	{
		DayNightCycle.RegisterShadow(this);
	}

	public override void _ExitTree()
	{
		DayNightCycle.UnregisterShadow(this);
	}
}
