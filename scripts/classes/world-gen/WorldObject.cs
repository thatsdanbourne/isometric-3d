using Godot;
using System;
using System.Collections.Generic;

public partial class WorldObject : Node3D
{
    [Signal] public delegate void ObjectBrokenEventHandler(WorldObject obj);

    [Export] public string ObjectType { get; set; }
    [Export] public float MaxHealth { get; set; } = 3.0f;
    [Export] public Godot.Collections.Array<Item> DropItems { get; set; } = new Godot.Collections.Array<Item>();
    [Export] public int DropCountMin { get; set; } = 2;
    [Export] public int DropCountMax { get; set; } = 4;
    [Export] public string HitSoundsKey { get; set; } = "hit_wood";

    private static readonly Dictionary<Texture2D, StandardMaterial3D> MaterialCache = new Dictionary<Texture2D, StandardMaterial3D>();
    private static readonly StandardMaterial3D BaseMaterial = GD.Load<StandardMaterial3D>("res://resources/materials/WorldObjectBase.tres");

    private Node3D visual;
    private Vector3 shakeOffset = Vector3.Zero;
    private Vector3 shakeVelocity = Vector3.Zero;

    public World World;
    public Chunk Chunk;

    private RandomNumberGenerator rng = new RandomNumberGenerator();
    private PackedScene pickupScene = ResourceLoader.Load<PackedScene>("res://scenes/ItemPickup.tscn");

    public float currentHealth { get; set; }


    public override void _Ready()
    {
        visual = GetNode<Node3D>("Sprite3D") ?? GetNode<Node3D>("AnimatedSprite3D");

        rng.Randomize();
        Translate(new Vector3(0f, 0f, rng.RandfRange(-0.01f, 0.01f)));
        currentHealth = MaxHealth;
        ApplySpriteMaterial();
        SetProcess(false);
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

        for (int i = 0; i < DropItems.Count; i++)
        {
            var item = DropItems[i];
            if (item == null) continue;

            int quantity = rng.RandiRange(DropCountMin, DropCountMax);
            
            for (int n = 0; n < quantity; n++)
            {
                ItemPickup pickup = pickupScene.Instantiate<ItemPickup>();
                pickup.Item = item;

                GetParent().AddChild(pickup);
                pickup.GlobalPosition = GlobalPosition;
            }
        }

        Cleanup();
        QueueFree();
    }

    public virtual void Cleanup()
    {
        if (World != null)
        {
            World.RemoveChunkObject(this);
        }
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
            tex = animSprite.SpriteFrames.GetFrameTexture("idle", 0);

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
}
