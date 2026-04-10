using System.Threading.Tasks;
using Godot;

public partial class WorldObject : Node3D, IToolHittable
{
	// [Signal]
	// public delegate void ObjectBrokenEventHandler(WorldObject obj);
	//
	// [Signal]
	// public delegate void ObjectHitFailedEventHandler(WorldObject obj);

	[Export] public string HitSoundsKey { get; set; } = "hit_wood";
	[Export] public string ObjectType { get; set; }

	public Vector3 WorldPosition;
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

	public Node3D GetHitRoot()
	{
		return this;
	}

	public ToolHitOutcome ReceiveToolHit(ToolItem tool, float damage, Vector3 fromDirection, Vector3 hitPoint)
	{
		return ApplyDamage(damage, fromDirection);
	}

	public ToolHitOutcome ApplyDamage(float amount, Vector3 fromDirection)
	{
		CurrentHealth -= amount;
		var destroyed = CurrentHealth <= 0;

		if (destroyed)
			BreakObject();

		AudioManager.Instance.PlayVariantAt(HitSoundsKey, GlobalPosition, AudioManager.BusWorld, 0.1f);

		return destroyed ? ToolHitOutcome.Destroyed : ToolHitOutcome.Hit;
	}

	public ToolHitOutcome ReceiveToolHitFailed(ToolItem tool, Vector3 fromDirection, Vector3 hitPoint)
	{
		AudioManager.Instance.PlayAt("hit_fail", GlobalPosition, 0.1f);
		return ToolHitOutcome.Failed;
	}

	private void BreakObject()
	{
		World.TryBreakObject(Data);
	}

	public float ModifyIncomingToolDamage(ToolItem tool, float damage, float baseDamage)
	{
		if (tool.DamageMultipliers != null && tool.DamageMultipliers.TryGetValue(ObjectType, out var mult))
			return baseDamage * mult;

		return baseDamage;
	}
}