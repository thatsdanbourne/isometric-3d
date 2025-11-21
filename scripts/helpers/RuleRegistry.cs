using Godot;
using System.Collections.Generic;

public partial class RuleRegistry : Node
{
    public List<ObjectPlacementRule> ObjectRules { get; private set; } = new();
    public List<BiomePlacementRule> BiomeRules { get; private set; } = new();

    private int terrainSeed;

    public RuleRegistry(int seed)
    {
        terrainSeed = seed;

        LoadObjectRules("res://resources/placement-rules/world-objects");
        LoadBiomeRules("res://resources/placement-rules/biomes");

        GD.Print($"RuleRegistry loaded {ObjectRules.Count} object rules.");
        GD.Print($"RuleRegistry loaded {BiomeRules.Count} biome rules.");
    }

    // ---------------------------------------------------------------------
    // OBJECT RULES
    // ---------------------------------------------------------------------
    private void LoadObjectRules(string path)
    {
        var dir = DirAccess.Open(path);
        if (dir == null)
        {
            GD.PushError("Could not open object rule folder: " + path);
            return;
        }

        dir.ListDirBegin();
        while (true)
        {
            string file = dir.GetNext();
            if (file == "")
                break;

            if (dir.CurrentIsDir())
                continue;

            if (!file.EndsWith(".tres"))
                continue;

            string resPath = path + "/" + file;

            var rule = ResourceLoader.Load<ObjectPlacementRule>(resPath);
            if (rule == null)
            {
                GD.PushWarning($"Skipping invalid object rule: {resPath}");
                continue;
            }

            int ruleSeed = terrainSeed + resPath.GetHashCode();
            rule.Init(ruleSeed);

            ObjectRules.Add(rule);
        }

        dir.ListDirEnd();
    }

    // ---------------------------------------------------------------------
    // BIOME RULES
    // ---------------------------------------------------------------------
    private void LoadBiomeRules(string path)
    {
        var dir = DirAccess.Open(path);
        if (dir == null)
        {
            GD.PushError("Could not open biome rule folder: " + path);
            return;
        }

        dir.ListDirBegin();
        while (true)
        {
            string file = dir.GetNext();
            if (file == "")
                break;

            if (dir.CurrentIsDir())
                continue;

            if (!file.EndsWith(".tres"))
                continue;

            string resPath = path + "/" + file;

            var biome = ResourceLoader.Load<BiomePlacementRule>(resPath);
            if (biome == null)
            {
                GD.PushWarning($"Skipping invalid biome rule: {resPath}");
                continue;
            }

            // All linked spawn rules inside biome are already typed C# resources.
            // No need to manually connect/dereference them.

            BiomeRules.Add(biome);
        }

        dir.ListDirEnd();
    }

    // ---------------------------------------------------------
    // BIOME LOOKUP
    // ---------------------------------------------------------
    public BiomePlacementRule GetBiome(float temp, float humidity)
    {
        foreach (var biome in BiomeRules)
        {
            if (biome.Matches(temp, humidity))
                return biome;
        }
        
        return BiomeRules.Find(b => b.Name.ToLower() == "plains");
    }
}