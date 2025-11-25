using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class BiomePlacementRule : Resource
{
    [Export] public string Name;

    [Export] public float MinTemp;
    [Export] public float MaxTemp;

    [Export] public float MinHumidity;
    [Export] public float MaxHumidity;

    [Export]public string GroundTileType;

    [Export]public Godot.Collections.Array<BiomeObjectSpawnRule> ObjectSpawnRules { get; set; }
        = new Godot.Collections.Array<BiomeObjectSpawnRule>();

    public bool Matches(float temp, float humidity)
    {
        return temp >= MinTemp && temp <= MaxTemp &&
               humidity >= MinHumidity && humidity <= MaxHumidity;
    }
}