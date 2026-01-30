using Godot;

public partial class WorldObject3D : WorldObjectBase
{
    [Export] public string HitSoundsKey { get; set; } = "hit_wood";

    public Vector3 WorldPosition;
    public Vector2I TileCoord;
    public bool MarkedForRemoval;

    public float currentHealth;

    public override void _Ready()
    {
        currentHealth = MaxHealth;
    }

    public override void Initialise(WorldObjectDefinition definition)
    {
        RequiredTier = definition.ToolTier;
        MaxHealth = definition.MaxHealth;
        currentHealth = MaxHealth;
    }

    public override void Reset()
    {
        MarkedForRemoval = false;
        currentHealth = MaxHealth;
    }

    public override void HitFailed()
    {
        EmitSignal(SignalName.ObjectHitFailed, this);
    }

    public override async void ApplyDamage(float amount, Vector3 fromDirection)
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
