using System.Collections.Generic;
using Godot;

public partial class TileRegistry
{
    private static readonly Dictionary<string, TileDefinition> _tiles = new();

    public static void Register(TileDefinition def)
    {
        _tiles[def.Name] = def;
    }

    public static TileDefinition GetByName(string id)
    {
        if (_tiles.TryGetValue(id, out var def))
            return def;

        GD.PushError($"TileRegistry: Tile with ID '{id}' not found.");
        return null;
    }

    public static TileDefinition GetById(int gridTileId)
    {
        foreach (var def in _tiles.Values)
        {
            if (def.GridTileId == gridTileId && def.IsWater == false)
                return def;
        }

        GD.PushError($"TileRegistry: Tile with GridTileId '{gridTileId}' not found.");
        return null;
    }

    public static bool Has(string id)
    {
        return _tiles.ContainsKey(id);
    }

    public static IEnumerable<TileDefinition> All => _tiles.Values;
}
