using Godot;

public partial class Debug : CanvasLayer
{
	private Label _fpsLabel;
	private Label _positionLabel;
	private Label _chunkLabel;
	private Label _biomeLabel;
	private float _updateTimer;

	public override void _Ready()
	{
		_fpsLabel = GetNode<Label>("MarginContainer/VBoxContainer/FPS");
		_positionLabel = GetNode<Label>("MarginContainer/VBoxContainer/Position");
		_chunkLabel = GetNode<Label>("MarginContainer/VBoxContainer/Chunk");
		_biomeLabel = GetNode<Label>("MarginContainer/VBoxContainer/Biome");
	}

	public override void _Process(double delta)
	{
		if (GameManager.Instance.LocalPlayer == null) return;

		_updateTimer += (float)delta;
		if (_updateTimer < 0.5f) return;

		_updateTimer = 0f;

		_fpsLabel.Text = $"{Engine.GetFramesPerSecond()}fps";

		var p = TileUtils.WorldToTile(GameManager.Instance.LocalPlayer.Position);
		_positionLabel.Text = $"x: {p.X}, y: {p.Y}";

		var c = TileUtils.WorldToChunk(GameManager.Instance.LocalPlayer.Position);
		_chunkLabel.Text = $"Chunk: {c.X}, {c.Y}";

		var biomeId = GameManager.Instance.LocalPlayer.CurrentBiome;

		_biomeLabel.Text = $"Biome: {biomeId}";
	}
}