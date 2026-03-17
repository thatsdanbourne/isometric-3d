using Godot;

public partial class DebugBiomeTeleporter : Node
{
	[Export] public int SearchRadius = 250;
	[Export] public bool UseFinalBiome = true;

	public Player Player;
	public World World;

	public override void _UnhandledInput(InputEvent @event)
	{
		// if (@event.IsActionPressed("debug_tp_plains"))
		// 	TeleportToBiome(BiomeId.Plains);
		//
		// if (@event.IsActionPressed("debug_tp_forest"))
		// 	TeleportToBiome(BiomeId.Forest)a;
		//
		// if (@event.IsActionPressed("debug_tp_taiga"))
		// 	TeleportToBiome(BiomeId.Taiga);

		if (@event.IsActionPressed("debug_tp_desert"))
			TeleportToBiome(BiomeId.Riverbank);

		// if (@event.IsActionPressed("debug_tp_tundra"))
		// 	TeleportToBiome(BiomeId.Tundra);
		//
		// if (@event.IsActionPressed("debug_tp_river"))
		// 	TeleportToBiome(BiomeId.River);
	}

	private void TeleportToBiome(BiomeId biomeId)
	{
		var playerChunk = TileManager.WorldToChunk(Player.GlobalPosition);

		var result = UseFinalBiome
			? World.BiomeSampler.FindNearestChunkWithFinalBiome(biomeId, playerChunk, SearchRadius)
			: World.BiomeSampler.FindNearestChunkWithBaseBiome(biomeId, playerChunk, SearchRadius);

		if (result == null)
		{
			GD.Print($"[DebugBiomeTeleporter] No biome '{biomeId}' found within radius {SearchRadius}.");
			return;
		}

		var targetChunk = result.Value;
		var targetTile = TileManager.GetChunkCenterTile(targetChunk);
		var targetWorld = TileManager.TileToWorld(targetTile);

		Player.GlobalPosition = targetWorld;

		GD.Print($"[DebugBiomeTeleporter] Teleported to '{biomeId}' at chunk {targetChunk}, tile {targetTile}.");
	}
}