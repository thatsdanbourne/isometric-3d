using Godot;

public partial class WorldObject : Node3D
{
    [Signal] public delegate void ObjectBrokenEventHandler(WorldObject obj);
    [Signal] public delegate void ObjectHitFailedEventHandler(WorldObject obj);

    [Export] public string HitSoundsKey { get; set; } = "hit_wood";
    [Export] public string ObjectType { get; set; }

    public Vector3 WorldPosition;
    public Vector2I TileCoord;
    public bool MarkedForRemoval;

    public float currentHealth;

    [Export] public Godot.Collections.Array<DropEntry> DropItems { get; set; } = new Godot.Collections.Array<DropEntry>();

    public ChunkObject Data;
    public World World;
    public ToolTier RequiredTier;
    public float MaxHealth;

    public override void _Ready()
    {
        currentHealth = MaxHealth;
    }

    public void Initialise(WorldObjectDefinition definition)
    {
        RequiredTier = definition.ToolTier;
        MaxHealth = definition.MaxHealth;
        currentHealth = MaxHealth;
    }

    public void Reset()
    {
        MarkedForRemoval = false;
        currentHealth = MaxHealth;
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
        currentHealth -= amount;
        if (currentHealth <= 0)
        {
            BreakObject();
        }
    }

    public void BreakObject()
    {
        EmitSignal(SignalName.ObjectBroken, this);
        World.WorldObjectManager.RequestBreak(Data);
    }
}
