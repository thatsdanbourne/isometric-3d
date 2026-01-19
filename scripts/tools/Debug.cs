using System;
using Godot;

public partial class Debug : CanvasLayer
{
	private Node3D world;
	private Player player;
	private Label fpsLabel;
	private Label positionLabel;
	private Label biomeLabel;

	private float updateTimer = 0f;

	public override void _Ready()
	{
		world = GetNode<Node3D>("../SubViewportContainer/SubViewport/World");
		fpsLabel = GetNode<Label>("MarginContainer/VBoxContainer/FPS");
		positionLabel = GetNode<Label>("MarginContainer/VBoxContainer/Position");
		biomeLabel = GetNode<Label>("MarginContainer/VBoxContainer/Biome");

		GameManager.Instance.LocalPlayerChanged += (Player p) =>
		{
			player = p;
		};
	}

	public override void _Process(double delta)
	{
		if (player == null) return;

		updateTimer += (float)delta;
		if (updateTimer < 0.3f) return;

		updateTimer = 0f;

		fpsLabel.Text = $"{Engine.GetFramesPerSecond()}fps";

		Vector3 p = player.Position;
		positionLabel.Text = $"x: {(int)p.X}, y: {(int)p.Z}";

		string biomeName = player.CurrentBiome;
		if (!string.IsNullOrEmpty(biomeName))
			biomeName = char.ToUpper(biomeName[0]) + biomeName.Substring(1);

		biomeLabel.Text = $"Biome: {biomeName}";
	}
}
