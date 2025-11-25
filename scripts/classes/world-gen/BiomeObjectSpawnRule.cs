using Godot;

[GlobalClass]
public partial class BiomeObjectSpawnRule : Resource
{
    [Export] public ObjectPlacementRule Rule;
    [Export] public float Density;

    public BiomeObjectSpawnRule() {}

    public BiomeObjectSpawnRule(ObjectPlacementRule rule, float density)
    {
        Rule = rule;
        Density = density;
    }
}