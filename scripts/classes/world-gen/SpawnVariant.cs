using Godot;

public class SpawnVariant
{
	public string Id { get; set; }
	public float Weight { get; set; } = 1f;

	public PackedScene Scene => WorldObjectRegistry.GetScene(Id);
}
