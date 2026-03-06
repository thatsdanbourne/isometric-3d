public class MobSpawnRule
{
	public string Id { get; set; }
	public string MobId { get; set; }

	public float ChunkChance { get; set; } = 0.2f;

	public int MinPerChunk { get; set; } = 1;
	public int MaxPerChunk { get; set; } = 3;
}