using Godot;

public class SpawnVariant(string id)
{
	public string Id { get; set; } = id;
	public int StableId { get; set; } = DeterministicHash.String32(id);
	public float Weight { get; set; } = 1f;

	public WorldObjectDefinition Definition
	{
		get
		{
			var def = WorldObjectRegistry.GetDefinition(StableId);
			if (def == null)
				GD.PushError($"SpawnVariant Id '{Id}' not found.");
			return def;
		}
	}
}