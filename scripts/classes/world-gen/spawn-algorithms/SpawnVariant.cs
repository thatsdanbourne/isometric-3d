using Godot;

public class SpawnVariant
{
	public string Id { get; set; }
	public float Weight { get; set; } = 1f;

	private int? _typeId;

	public int TypeId => _typeId ??= WorldObjectRegistry.StableHash(Id);

	public WorldObjectDefinition Definition
	{
		get
		{
			var def = WorldObjectRegistry.GetDefinition(TypeId);
			if (def == null)
				GD.PushError($"SpawnVariant Id '{Id}' not found.");
			return def;
		}
	}
}