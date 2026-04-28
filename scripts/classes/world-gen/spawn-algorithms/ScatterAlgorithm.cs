using Godot;

public class ScatterAlgorithm : SpawnAlgorithm
{
	private readonly FastNoiseLite _maskNoise;
	private readonly FastNoiseLite _rollNoise;

	private readonly Vector2I _worldOffset;
	private readonly float _scatterPower;

	public ScatterAlgorithm(
		int seed,
		Vector2I worldOffset,
		FastNoiseLite.NoiseTypeEnum noiseType,
		float frequency,
		float scatterPower = 1.75f,
		int octaves = 2,
		float gain = 0.5f,
		float lacunarity = 2f)
	{
		_worldOffset = worldOffset;
		_scatterPower = scatterPower;

		_maskNoise = new FastNoiseLite
		{
			Seed = seed,
			NoiseType = noiseType,
			Frequency = frequency,
			FractalOctaves = octaves,
			FractalGain = gain,
			FractalLacunarity = lacunarity
		};

		_rollNoise = new FastNoiseLite
		{
			Seed = seed + 918273,
			NoiseType = FastNoiseLite.NoiseTypeEnum.Value,
			Frequency = 0.85f
		};
	}

	public override bool ShouldPlace(int x, int z, float density)
	{
		x += _worldOffset.X;
		z += _worldOffset.Y;

		density = Mathf.Clamp(density, 0f, 1f);
		if (density <= 0f)
			return false;

		var mask = _maskNoise.GetNoise2D(x, z);
		var mask01 = (mask + 1f) * 0.5f;

		var shapedMask = Mathf.Pow(mask01, _scatterPower);
		var localChance = density * shapedMask;

		var roll = _rollNoise.GetNoise2D(x, z);
		var roll01 = (roll + 1f) * 0.5f;

		return roll01 < localChance;
	}
}