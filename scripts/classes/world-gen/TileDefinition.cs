using System.Collections.Generic;

public partial class TileDefinition
{
    public string Name;
    public int GridTileId;
    public bool IsWater = false;

    public List<DetailMeshRule> DetailMeshes = new();
}
