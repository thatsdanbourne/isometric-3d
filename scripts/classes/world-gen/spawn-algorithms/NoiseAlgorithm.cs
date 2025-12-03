using Godot;

public class NoiseAlgorithm : SpawnAlgorithm
{
	public FastNoiseLite.NoiseTypeEnum BaseNoiseType { get; }
	public float BaseNoiseFrequency { get; }
	public float BaseNoiseThreshold { get; }
	public int BaseNoiseOctaves { get; }
	public float BaseNoiseGain { get; }
	public float BaseNoiseLacunarity { get; }

	public bool UseDetailNoise { get; }
	public FastNoiseLite.NoiseTypeEnum DetailNoiseType { get; }
	public float DetailNoiseFrequency { get; }
	public float DetailNoiseThreshold { get; }
	public int DetailNoiseOctaves { get; }
	public float DetailNoiseGain { get; }
	public float DetailNoiseLacunarity { get; }

	public Vector2I WorldOffset { get; set; }

	private readonly FastNoiseLite baseNoise;
	private readonly FastNoiseLite detailNoise;

	public NoiseAlgorithm(
		int seed,
		Vector2I worldOffset,
		FastNoiseLite.NoiseTypeEnum baseNoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
		float baseNoiseFrequency = 0.01f,
		float baseNoiseThreshold = 0.5f,
		int baseNoiseOctaves = 5,
		float baseNoiseGain = 0.5f,
		float baseNoiseLacunarity = 2.0f,
		bool useDetailNoise = true,
		FastNoiseLite.NoiseTypeEnum detailNoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,
		float detailNoiseFrequency = 0.1f,
		float detailNoiseThreshold = 0.5f,
		int detailNoiseOctaves = 5,
		float detailNoiseGain = 0.5f,
		float detailNoiseLacunarity = 2.0f
	)
	{
		WorldOffset = worldOffset;

		BaseNoiseType = baseNoiseType;
		BaseNoiseFrequency = baseNoiseFrequency;
		BaseNoiseThreshold = baseNoiseThreshold;
		BaseNoiseOctaves = baseNoiseOctaves;
		BaseNoiseGain = baseNoiseGain;
		BaseNoiseLacunarity = baseNoiseLacunarity;

		UseDetailNoise = useDetailNoise;
		DetailNoiseType = detailNoiseType;
		DetailNoiseFrequency = detailNoiseFrequency;
		DetailNoiseThreshold = detailNoiseThreshold;
		DetailNoiseOctaves = detailNoiseOctaves;
		DetailNoiseGain = detailNoiseGain;
		DetailNoiseLacunarity = detailNoiseLacunarity;

		// Base noise setup
		baseNoise = new FastNoiseLite();
		baseNoise.Seed = seed;
		baseNoise.NoiseType = BaseNoiseType;
		baseNoise.Frequency = BaseNoiseFrequency;
		baseNoise.FractalOctaves = BaseNoiseOctaves;
		baseNoise.FractalGain = BaseNoiseGain;
		baseNoise.FractalLacunarity = BaseNoiseLacunarity;

		// Optional detail noise
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

	public override bool ShouldPlace(int x, int z, float density)
	{
		// Apply world offset (same as your old WorldOffset logic)
		x += WorldOffset.X;
		z += WorldOffset.Y;

		density = Mathf.Clamp(density, 0f, 1f);
		if (density <= 0f)
			return false;

		// 1) Random-ish pre-filter using base noise
		float rand = baseNoise.GetNoise2D(x + 12345, z + 67890);
		rand = (rand + 1f) * 0.5f; // map [-1,1] to [0,1]

		if (rand > density)
			return false;

		// 2) Base noise threshold
		float baseVal = baseNoise.GetNoise2D(x, z); // [-1,1]
													// Same trick: threshold scaled by density
		if (baseVal < (BaseNoiseThreshold * density))
			return false;

		// 3) Optional detail noise
		if (UseDetailNoise && detailNoise != null)
		{
			float detailVal = detailNoise.GetNoise2D(x, z); // [-1,1]
			if (detailVal < (DetailNoiseThreshold * density))
				return false;
		}

		return true;
	}
}
