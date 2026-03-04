public class MobSpawnRule
{
	public string Id { get; set; }
	public float Density { get; set; } = 0.1f;
	public string MobId { get; set; }

	public NoiseAlgorithm Algorithm;

	public int MinPerChunk { get; set; } = 0;
	public int MaxPerChunk { get; set; } = 3;
}