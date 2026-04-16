using Godot;

public partial class Debug : CanvasLayer
{
	private Label fpsLabel;
	private Label positionLabel;
	private Label biomeLabel;
	private float updateTimer = 0f;

	public override void _Ready()
	{
		fpsLabel = GetNode<Label>("MarginContainer/VBoxContainer/FPS");
		positionLabel = GetNode<Label>("MarginContainer/VBoxContainer/Position");
		biomeLabel = GetNode<Label>("MarginContainer/VBoxContainer/Biome");
	}

	public override void _Process(double delta)
	{
		if (GameManager.Instance.LocalPlayer == null) return;

		updateTimer += (float)delta;
		if (updateTimer < 0.3f) return;

		updateTimer = 0f;

		fpsLabel.Text = $"{Engine.GetFramesPerSecond()}fps";

		var p = TileUtils.WorldToTile(GameManager.Instance.LocalPlayer.Position);
		positionLabel.Text = $"x: {p.X}, y: {p.Y}";

		var biomeId = GameManager.Instance.LocalPlayer.CurrentBiome;

		biomeLabel.Text = $"Biome: {biomeId}";
	}
}