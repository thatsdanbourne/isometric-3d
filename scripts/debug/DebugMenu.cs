using System;
using Godot;
using ImGuiNET;

public partial class DebugMenu : Node
{
	private World _world;
	private Player LocalPlayer => GameManager.Instance.LocalPlayer;

	private bool _showDebugInfo;
	private int _selectedBiomeIndex;

	public int SearchRadius = 250;

	public override void _Ready()
	{
		_world = GetNode<World>("/root/Bootstrap/World");
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("toggle_debug"))
			_showDebugInfo = !_showDebugInfo;
	}

	public override void _Process(double delta)
	{
		if (!_showDebugInfo) return;

		ImGui.Begin("Debug Tools");
		ImGui.Text("Time of Day");
		ImGui.Text("Biome Teleporter");
		var selectedBiome = (BiomeId)_selectedBiomeIndex;
		var preview = selectedBiome.ToString();

		if (ImGui.BeginCombo("Target Biome", preview))
		{
			for (var i = 0; i < Enum.GetNames(typeof(BiomeId)).Length; i++)
			{
				var biome = (BiomeId)i;
				var isSelected = biome == selectedBiome;

				if (ImGui.Selectable(biome.ToString(), isSelected))
					_selectedBiomeIndex = i;

				if (isSelected)
					ImGui.SetItemDefaultFocus();
			}

			ImGui.EndCombo();
		}

		if (ImGui.Button("Teleport"))
		{
			var biome = (BiomeId)_selectedBiomeIndex;
			TeleportToBiome(biome);
		}

		ImGui.End();
	}

	private void TeleportToBiome(BiomeId biomeId)
	{
		var playerChunk = TileUtils.WorldToChunk(LocalPlayer.GlobalPosition);

		var result = _world.BiomeSampler.FindNearestChunkWithFinalBiome(biomeId, playerChunk, SearchRadius);

		if (result == null)
		{
			GD.Print($"[DEBUG] No biome '{biomeId}' found within radius {SearchRadius}.");
			return;
		}

		var targetChunk = result.Value;
		var targetTile = TileUtils.GetChunkCenterTile(targetChunk);
		var targetWorld = TileUtils.TileToWorld(targetTile);

		LocalPlayer.GlobalPosition = targetWorld;

		GD.Print($"[DebugBiomeTeleporter] Teleported to '{biomeId}' at chunk {targetChunk}, tile {targetTile}.");
	}
}