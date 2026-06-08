using Godot;

public class SpawnVariant
{
	public string Id { get; set; }
	public int StableId { get; set; }
	public float Weight { get; set; } = 1f;

	public SpawnVariant(string id)
	{
		Id = id;
		StableId = DeterministicHash.String32(id);
	}

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