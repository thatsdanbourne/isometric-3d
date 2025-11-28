using Godot;

[GlobalClass]
public partial class BiomeObjectSpawnRule : Resource
{
    [Export] public ObjectPlacementRule Rule;
    [Export] public float Density = 0.5f;
    [Export] public Godot.Collections.Array<ObjectVariant> AllowedVariants;

    public BiomeObjectSpawnRule() {}

    public BiomeObjectSpawnRule(ObjectPlacementRule rule, float density)
    {
        Rule = rule;
        Density = density;
    }
}