using System.Collections.Generic;

public partial class DetailMeshRegistry
{
    private static Dictionary<string, DetailMeshDefinition> _defs = new();

    public static void Register(DetailMeshDefinition def)
    {
        _defs[def.Id] = def;
    }

    public static DetailMeshDefinition Get(string id)
    {
        return _defs[id];
    }
}
