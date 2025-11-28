using Godot;

[GlobalClass]
public partial class DecorPlacementRule : Resource
{
    [Export] public string Name { get; set; }
    [Export] public PackedScene Scene { get; set; }

    [ExportGroup("Base Noise")]
    [Export] public FastNoiseLite.NoiseTypeEnum BaseNoiseType { get; set; } = FastNoiseLite.NoiseTypeEnum.SimplexSmooth;
    [Export] public float BaseNoiseFrequency { get; set; } = 0.01f;
    [Export] public float BaseNoiseThreshold { get; set; } = 0.5f;
    [Export] public int BaseNoiseOctaves { get; set; } = 5;
    [Export] public float BaseNoiseGain { get; set; } = 0.5f;
    [Export] public float BaseNoiseLacunarity { get; set; } = 2.0f;

    [ExportGroup("Detail Noise")]
    [Export] public bool UseDetailNoise { get; set; } = true;
    [Export] public FastNoiseLite.NoiseTypeEnum DetailNoiseType { get; set; } = FastNoiseLite.NoiseTypeEnum.Perlin;
    [Export] public float DetailNoiseFrequency { get; set; } = 0.1f;
    [Export] public float DetailNoiseThreshold { get; set; } = 0.5f;
    [Export] public int DetailNoiseOctaves { get; set; } = 5;
    [Export] public float DetailNoiseGain { get; set; } = 0.5f;
    [Export] public float DetailNoiseLacunarity { get; set; } = 2.0f; 

    public Vector2I WorldOffset;

    private FastNoiseLite baseNoise;
    private FastNoiseLite detailNoise;
    private RandomNumberGenerator rng;

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

    public bool ShouldPlace(int x, int z)
    {
        float nx = x + WorldOffset.X;
        float nz = z + WorldOffset.Y;

        float baseValue = baseNoise.GetNoise2D(nx, nz);
        if (baseValue < BaseNoiseThreshold)
            return false;

        if (UseDetailNoise)
        {
            float detailValue = detailNoise.GetNoise2D(nx, nz);
            if (detailValue < DetailNoiseThreshold)
                return false;
        }

        return true;
    }
}
