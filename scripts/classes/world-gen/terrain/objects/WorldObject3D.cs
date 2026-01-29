using Godot;

public partial class WorldObject3D : WorldObjectBase
{
    [Export] public Godot.Collections.Array<DropEntry> DropItems { get; set; } = new Godot.Collections.Array<DropEntry>();
    [Export] public string HitSoundsKey { get; set; } = "hit_wood";

    public Vector3 WorldPosition;
    public Vector2I TileCoord;
    public bool MarkedForRemoval;

    private RandomNumberGenerator rng = new RandomNumberGenerator();
    private PackedScene pickupScene = ResourceLoader.Load<PackedScene>("res://scenes/ItemPickup.tscn");

    public float currentHealth;

    public override void _Ready()
    {
        rng.Randomize();
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

        if (DropItems == null) return;

        foreach (var entry in DropItems)
        {
            if (GD.Randf() > entry.Chance)
                continue;

            var item = ItemRegistry.GetItem(entry.ItemId);
            if (item == null) continue;

            int quantity = rng.RandiRange(entry.MinQuantity, entry.MaxQuantity);

            for (int n = 0; n < quantity; n++)
            {
                ItemPickup pickup = pickupScene.Instantiate<ItemPickup>();
                pickup.Item = item;

                GetParent().AddChild(pickup);
                pickup.GlobalPosition = GlobalPosition;
            }
        }

        World.WorldObjectManager.RequestBreak(Data);
    }
}
