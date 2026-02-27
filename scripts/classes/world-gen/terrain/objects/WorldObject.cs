using Godot;

public partial class WorldObject : Node3D
{
	[Signal]
	public delegate void ObjectBrokenEventHandler(WorldObject obj);

	[Signal]
	public delegate void ObjectHitFailedEventHandler(WorldObject obj);

	[Export] public string HitSoundsKey { get; set; } = "hit_wood";
	[Export] public string ObjectType { get; set; }

	public Vector3 WorldPosition;
	public Vector2I TileCoord;
	public bool MarkedForRemoval;

	public float CurrentHealth;

	[Export] public Godot.Collections.Array<DropEntry> DropItems { get; set; } = new();

	public ChunkObject Data;
	public World World;
	public ToolTier RequiredTier;
	public float MaxHealth;

	public override void _Ready()
	{
		CurrentHealth = MaxHealth;
		SetProcess(false);
		SetPhysicsProcess(false);
	}

	public void Initialise(WorldObjectDefinition definition)
	{
		MarkedForRemoval = false;
		CurrentHealth = MaxHealth;
		RequiredTier = definition.ToolTier;
		MaxHealth = definition.MaxHealth;
		CurrentHealth = MaxHealth;
	}

	public void HitFailed()
	{
		EmitSignal(SignalName.ObjectHitFailed, this);
	}

	public async void ApplyDamage(float amount, Vector3 fromDirection)
	{
		await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
		AudioManager.Instance.PlayVariantAt(HitSoundsKey, GlobalPosition, 0.1f);
		// ApplyHitShake(fromDirection);
		CurrentHealth -= amount;
		if (CurrentHealth <= 0) BreakObject();
	}

	public void BreakObject()
	{
		EmitSignal(SignalName.ObjectBroken, this);
		World.WorldObjectManager.RequestBreak(Data);
	}
}