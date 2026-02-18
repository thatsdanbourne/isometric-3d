using System.Collections.Generic;
using Godot;

public static class TileRegistry
{
	private static readonly Dictionary<string, TileDefinition> Tiles = new();

	public static void Register(TileDefinition def)
	{
		Tiles[def.Name] = def;
	}

	public static TileDefinition GetByName(string id)
	{
		if (Tiles.TryGetValue(id, out var def))
			return def;

		GD.PushError($"TileRegistry: Tile with ID '{id}' not found.");
		return null;
	}

	public static TileDefinition GetById(int gridTileId)
	{
		foreach (var def in Tiles.Values)
			if (def.GridTileId == gridTileId && !def.IsWater)
				return def;

		GD.PushError($"TileRegistry: Tile with GridTileId '{gridTileId}' not found.");
		return null;
	}

	public static bool Has(string id)
	{
		return Tiles.ContainsKey(id);
	}

	public static IEnumerable<TileDefinition> All => Tiles.Values;
}