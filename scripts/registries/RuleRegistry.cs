using Godot;
using System.Collections.Generic;

public partial class RuleRegistry : Node
{
    public List<ObjectPlacementRule> ObjectRules { get; private set; } = new();
    public List<BiomePlacementRule> BiomeRules { get; private set; } = new();

    private int terrainSeed;

    public RuleRegistry(int seed, Vector2I worldOffset)
    {
        terrainSeed = seed;
        GD.Print("Checking Biome Path Exists: " + DirAccess.DirExistsAbsolute("res://resources/placement-rules/biomes"));
        LoadObjectRules("res://resources/placement-rules/world-objects");
        LoadBiomeRules("res://resources/placement-rules/biomes");

        foreach (var rule in ObjectRules)
        {
            rule.WorldOffset = worldOffset;
        }

        GD.Print($"RuleRegistry loaded {ObjectRules.Count} object rules.");
        GD.Print($"RuleRegistry loaded {BiomeRules.Count} biome rules.");
    }

    // ---------------------------------------------------------------------
    // OBJECT RULES
    // ---------------------------------------------------------------------
    private void LoadObjectRules(string path)
{
    // 💡 New Godot 4.3+ method for listing files in a packaged resource path
    var files = DirAccess.GetFilesAt(path); 

    if (files == null || files.Length == 0)
    {
        // This will now correctly trigger if the folder is empty or not found in the PCK
        GD.PushError("Could not open object rule folder or folder is empty: " + path);
        return;
    }

    foreach (var file in files)
    {
        // Check to ensure you only load .tres files
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
}

    // ---------------------------------------------------------------------
    // BIOME RULES
    // ---------------------------------------------------------------------
    private void LoadBiomeRules(string path)
{
    // 💡 New Godot 4.3+ method for listing files in a packaged resource path
    var files = DirAccess.GetFilesAt(path); 

    if (files == null || files.Length == 0)
    {
        GD.PushError("Could not open biome rule folder or folder is empty: " + path);
        return;
    }

    foreach (var file in files)
    {
        if (!file.EndsWith(".tres"))
            continue;

        string resPath = path + "/" + file;

        var biome = ResourceLoader.Load<BiomePlacementRule>(resPath);
        if (biome == null)
        {
            GD.PushWarning($"Skipping invalid biome rule: {resPath}");
            continue;
        }

        // All linked spawn rules... (rest of your original logic)

        BiomeRules.Add(biome);
    }
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