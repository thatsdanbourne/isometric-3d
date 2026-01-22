using Godot;

public partial class Display : Control
{
	[Export] public SubViewport viewport;
	Sprite2D sprite;
	CameraController cameraController;

	public override void _Ready()
	{
		sprite = GetNode<Sprite2D>("Sprite2D");
	}

	public override void _Process(double delta)
	{
		Vector2I ScreenSize = GetWindow().Size;
		Vector2I GameSize = viewport.Size - new Vector2I(2, 2);
		Vector2 scale = ScreenSize / GameSize;

		float DisplayScaleMin = Mathf.Min(scale.X, scale.Y);
		sprite.Scale = new Vector2(DisplayScaleMin, DisplayScaleMin);

		if (cameraController != null)
		{
			Vector2 texelError = cameraController.TexelError;
			Vector2 pixelError = texelError * sprite.Scale;
			sprite.Position = -sprite.Scale + pixelError;
		}
	}

	public void SetCameraController(CameraController controller)
	{
		cameraController = controller;
	}
}
