public class MobSpawnRule
{
	public string Id { get; set; }
	public int StableId { get; set; }

	public string MobId { get; set; }

	public float ChunkChance { get; set; } = 0.2f;

	public int MinPerChunk { get; set; } = 1;
	public int MaxPerChunk { get; set; } = 3;

	public MobSpawnRule(string id)
	{
		Id = id;
		StableId = DeterministicHash.String32(id);
	}
}