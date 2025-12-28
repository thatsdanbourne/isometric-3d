using Godot;
using System.Collections.Generic;

public partial class WorldObject : Node3D
{
    [Signal] public delegate void ObjectBrokenEventHandler(WorldObject obj);
    [Signal] public delegate void ObjectHitFailedEventHandler(WorldObject obj);

    [Export] public string ObjectType { get; set; }
    [Export] public Godot.Collections.Array<DropEntry> DropItems { get; set; } = new Godot.Collections.Array<DropEntry>();
    [Export] public string HitSoundsKey { get; set; } = "hit_wood";

    private static readonly Dictionary<Texture2D, StandardMaterial3D> MaterialCache = new Dictionary<Texture2D, StandardMaterial3D>();
    private static readonly StandardMaterial3D BaseMaterial = GD.Load<StandardMaterial3D>("res://resources/materials/WorldObjectBase.tres");

    protected Node3D visual;
    private CollisionShape3D collisionShape;

    private Vector3 shakeOffset = Vector3.Zero;
    private Vector3 shakeVelocity = Vector3.Zero;

    public World World;
    public Vector3 WorldPosition;
    public Vector2I TileCoord;
    public ChunkObject Data;
    public bool MarkedForRemoval;

    public ToolTier RequiredTier;
    public float MaxHealth;

    private RandomNumberGenerator rng = new RandomNumberGenerator();
    private PackedScene pickupScene = ResourceLoader.Load<PackedScene>("res://scenes/ItemPickup.tscn");

    public float currentHealth;


    public override void _Ready()
    {
        visual = GetNodeOrNull<Node3D>("Sprite3D") ?? GetNodeOrNull<Node3D>("AnimatedSprite3D");
        collisionShape = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");

        rng.Randomize();
        currentHealth = MaxHealth;
        ApplySpriteMaterial();
        SetProcess(false);
    }

    public void Initialize(WorldObjectDefinition definition)
    {
        RequiredTier = definition.ToolTier;
        MaxHealth = definition.MaxHealth;
        currentHealth = MaxHealth;
    }

    public void Reset()
    {
        MarkedForRemoval = false;
        currentHealth = MaxHealth;
        shakeOffset = Vector3.Zero;
        shakeVelocity = Vector3.Zero;
        SetProcess(false);
    }

    public void HitFailed()
    {
        EmitSignal(SignalName.ObjectHitFailed, this);
    }

    public async virtual void ApplyDamage(float amount, Vector3 fromDirection)
    {
        await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
        AudioManager.Instance.PlayVariantAt(HitSoundsKey, GlobalPosition, 0.1f);
        ApplyHitShake(fromDirection);
        currentHealth -= amount;
        if (currentHealth <= 0)
        {
            BreakObject();
        }
    }

    public void ApplyHitShake(Vector3 fromDirection)
    {
        float tiltDirection = fromDirection.X >= 0f ? 1f : -1f;
        float intensity = 4f;
        shakeVelocity += new Vector3(tiltDirection * intensity, 0f, 0f);

        SetProcess(true);
    }

    public virtual void BreakObject()
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

        if (World.ActiveChunks.TryGetValue(Data.ChunkCoord, out var chunk))
            chunk.Objects.Remove(Data);

        World.WorldObjectManager.EnqueueRemoval(Data);
    }

    private void ApplySpriteMaterial()
    {
        // Find a Sprite3D or AnimatedSprite3D
        if (visual == null)
            return;

        Texture2D tex = null;

        if (visual is Sprite3D sprite3D)
            tex = sprite3D.Texture;
        else if (visual is AnimatedSprite3D animSprite)
            tex = animSprite.SpriteFrames.GetFrameTexture("default", 0);

        if (tex == null)
            return;

        if (!MaterialCache.TryGetValue(tex, out StandardMaterial3D mat))
        {
            mat = (StandardMaterial3D)BaseMaterial.Duplicate();
            mat.AlbedoTexture = tex;
            MaterialCache[tex] = mat;
        }

        if (visual is Sprite3D s3d)
        {
            s3d.MaterialOverride = mat;
            s3d.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        else if (visual is AnimatedSprite3D as3d)
        {
            as3d.MaterialOverride = mat;
            as3d.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        shakeVelocity = shakeVelocity.MoveToward(Vector3.Zero, 10f * dt);
        shakeOffset += shakeVelocity * dt;

        float tiltStrength = 100f;
        Vector3 shakeRotation = new Vector3(
            0f,
            0f,
            -shakeOffset.X * tiltStrength
        );

        visual.RotationDegrees = shakeRotation;

        shakeOffset = shakeOffset.MoveToward(Vector3.Zero, 5f * dt);

        bool isShaking = shakeVelocity.LengthSquared() > 0.0001f || shakeOffset.LengthSquared() > 0.0001f;
        if (!isShaking)
        {
            visual.RotationDegrees = Vector3.Zero;
            shakeOffset = Vector3.Zero;
            SetProcess(false);
            return;
        }
    }

    public void EnableCollision()
    {
        if (collisionShape == null) return;

        collisionShape.Disabled = false;
    }

    public void DisableCollision()
    {
        if (collisionShape == null) return;

        collisionShape.Disabled = true;
    }

    public virtual void Interact(Player player) { }
    public virtual void SetHighlighted(bool highlighted) { }
}
