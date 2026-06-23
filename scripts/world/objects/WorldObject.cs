using Godot;

public partial class WorldObject : Node3D, IToolHittable
{
	[Export] public string ObjectType { get; set; }
	[Export] public Godot.Collections.Array<DropEntry> DropItems { get; set; } = new();
	[Export] public string HitSoundsKey = "hit_wood";
	[Export] public string BreakSoundsKey = "break_wood";
	[Export] public string FailedHitSoundsKey = "hit_fail";

	public Vector3 WorldPosition;
	public bool MarkedForRemoval;

	public float CurrentHealth;

	public ChunkObject Data;
	public World World;
	public ToolTier RequiredTier;
	public float MaxHealth;

	public virtual bool CanReceiveToolHits => true;

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

	public ToolHitResponse ReceiveToolHit(ToolItem tool, float damage, float knockback, float stagger,
		Vector3 fromDirection,
		Vector3 hitPoint)
	{
		return ApplyDamage(damage, fromDirection);
	}

	public ToolHitResponse ApplyDamage(float amount, Vector3 fromDirection)
	{
		CurrentHealth -= amount;
		var destroyed = CurrentHealth <= 0;

		if (destroyed)
			BreakObject();

		return destroyed ? ToolHitResponse.Destroyed() : ToolHitResponse.Hit();
	}

	public ToolHitResponse ReceiveToolHitFailed(ToolItem tool, Vector3 fromDirection, Vector3 hitPoint)
	{
		return ToolHitResponse.Failed();
	}

	protected void BreakObject()
	{
		World.TryBreakObject(Data);
	}

	public float ModifyIncomingToolDamage(ToolItem tool, float damage, float baseDamage)
	{
		if (tool.DamageMultipliers != null && tool.DamageMultipliers.TryGetValue(ObjectType, out var mult))
			return baseDamage * mult;

		return baseDamage;
	}

	public void OnStagger()
	{
	}

	public string GetImpactType()
	{
		return ObjectType;
	}

	public string GetHitSound(ToolItem tool)
	{
		return HitSoundsKey;
	}

	public string GetBreakSound()
	{
		return BreakSoundsKey;
	}

	public string GetFailedHitSound()
	{
		return FailedHitSoundsKey;
	}
}
