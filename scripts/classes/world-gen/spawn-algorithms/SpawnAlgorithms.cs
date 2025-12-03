using Godot;

public partial class SpawnAlgorithms
{
	public static SpawnAlgorithm NoiseLikeObjectPlacement(int seed, Vector2I worldOffset,
	   float baseFreq = 0.01f, float baseThreshold = 0.5f,
	   float detailFreq = 0.1f, float detailThreshold = 0.5f)
	{
		return new NoiseAlgorithm(
			seed,
			worldOffset,
			baseNoiseFrequency: baseFreq,
			baseNoiseThreshold: baseThreshold,
			detailNoiseFrequency: detailFreq,
			detailNoiseThreshold: detailThreshold
		);
	}
}
