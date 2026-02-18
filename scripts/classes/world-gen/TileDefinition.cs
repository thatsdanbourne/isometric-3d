using System.Collections.Generic;

public class TileDefinition
{
    public string Name;
    public int GridTileId;
    public bool IsWater = false;

    public readonly List<DetailMeshRule> DetailMeshes = new();
}
