using Godot;

[GlobalClass]
public partial class ObjectPlacementRule : Resource
{
    // Inspector Properties ---------------------------------------

    [Export] public string Name { get; set; }
    [Export] public PackedScene Scene { get; set; }

    [Export] public FastNoiseLite.NoiseTypeEnum BaseNoiseType { get; set; } = FastNoiseLite.NoiseTypeEnum.SimplexSmooth;
    [Export] public float BaseNoiseFrequency { get; set; } = 0.01f;
    [Export] public float BaseNoiseThreshold { get; set; } = 0.5f;
    [Export] public int BaseNoiseOctaves { get; set; } = 5;
    [Export] public float BaseNoiseGain { get; set; } = 0.5f;
    [Export] public float BaseNoiseLacunarity { get; set; } = 2.0f;

    [Export] public bool UseDetailNoise { get; set; } = true;
    [Export] public FastNoiseLite.NoiseTypeEnum DetailNoiseType { get; set; } = FastNoiseLite.NoiseTypeEnum.Perlin;
    [Export] public float DetailNoiseFrequency { get; set; } = 0.1f;
    [Export] public float DetailNoiseThreshold { get; set; } = 0.5f;
    [Export] public int DetailNoiseOctaves { get; set; } = 5;
    [Export] public float DetailNoiseGain { get; set; } = 0.5f;
    [Export] public float DetailNoiseLacunarity { get; set; } = 2.0f;

    // Runtime state ----------------------------------------------
    public Vector2I WorldOffset;

    private FastNoiseLite baseNoise;
    private FastNoiseLite detailNoise;
    private RandomNumberGenerator rng;

    // Call this from RuleRegistry after loading the .tres
    public void Init(int seed)
    {
        rng = new RandomNumberGenerator();
        rng.Seed = (ulong)seed;

        baseNoise = new FastNoiseLite();
        baseNoise.Seed = seed;
        baseNoise.NoiseType = BaseNoiseType;
        baseNoise.Frequency = BaseNoiseFrequency;
        baseNoise.FractalOctaves = BaseNoiseOctaves;
        baseNoise.FractalGain = BaseNoiseGain;
        baseNoise.FractalLacunarity = BaseNoiseLacunarity;

        if (UseDetailNoise)
        {
            detailNoise = new FastNoiseLite();
            detailNoise.Seed = seed + 1;
            detailNoise.NoiseType = DetailNoiseType;
            detailNoise.Frequency = DetailNoiseFrequency;
            detailNoise.FractalOctaves = DetailNoiseOctaves;
            detailNoise.FractalGain = DetailNoiseGain;
            detailNoise.FractalLacunarity = DetailNoiseLacunarity;
        }
    }

    public bool ShouldPlace(int x, int z, float density)
    {
        x += WorldOffset.X;
        z += WorldOffset.Y;

        density = Mathf.Clamp(density, 0f, 1f);
        if (density <= 0)
            return false;

        float rand = baseNoise.GetNoise2D(x + 12345, z + 67890);
        rand = (rand + 1f) * 0.5f;
        
        if (rand > density)
            return false;

        // Base noise check
        float baseVal = baseNoise.GetNoise2D(x, z);
        if (baseVal < (BaseNoiseThreshold * density))
            return false;

        // Detail noise check
        if (UseDetailNoise)
        {
            float detailVal = detailNoise.GetNoise2D(x, z);
            if (detailVal < (DetailNoiseThreshold * density))
                return false;
        }

        return true;
    }
}