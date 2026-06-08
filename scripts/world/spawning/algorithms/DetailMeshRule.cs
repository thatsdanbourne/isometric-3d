public class DetailMeshRule
{
	public string MeshId { get; init; }
	public float Density { get; init; } = 1f;

	public int MinPerTile { get; init; } = 1;
	public int MaxPerTile { get; init; } = 1;

	public float MinScale { get; init; } = 1f;
	public float MaxScale { get; init; } = 1f;
}