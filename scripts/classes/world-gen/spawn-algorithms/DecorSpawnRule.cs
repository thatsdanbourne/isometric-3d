using Godot;

public class DecorSpawnRule
{
	public SpawnAlgorithm Algorithm { get; set; }
	public float Density { get; set; } = 0.2f;
	public string DecorId { get; set; }

	public bool ShouldPlace(int x, int z)
	{
		return Algorithm.ShouldPlace(x, z, Density);
	}
}
