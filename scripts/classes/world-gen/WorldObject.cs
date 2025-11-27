using Godot;
using System.Collections.Generic;

public partial class WorldObject : Node3D
{
    [Signal] public delegate void ObjectBrokenEventHandler(WorldObject obj);

    [Export] public string ObjectType { get; set; }
    [Export] public float MaxHealth { get; set; } = 3.0f;
    [Export] public Godot.Collections.Array<Item> DropItems { get; set; } = new Godot.Collections.Array<Item>();
    [Export] public int DropCountMin { get; set; } = 2;
    [Export] public int DropCountMax { get; set; } = 4;
    [Export] public Godot.Collections.Array<AudioStream> HitSounds { get; set; } = new Godot.Collections.Array<AudioStream>();

    private static readonly Dictionary<Texture2D, StandardMaterial3D> MaterialCache = new Dictionary<Texture2D, StandardMaterial3D>();
    private static readonly StandardMaterial3D BaseMaterial = GD.Load<StandardMaterial3D>("res://resources/materials/WorldObjectBase.tres");

    public World World;
    public Chunk Chunk;

    private GodotObject AudioManager;

    private RandomNumberGenerator rng = new RandomNumberGenerator();
    private PackedScene pickupScene = ResourceLoader.Load<PackedScene>("res://scenes/ItemPickup.tscn");

    public float currentHealth { get; set; }

    public override void _Ready()
    {
        AudioManager = GetNode("/root/AudioManager");

        rng.Randomize();
        Translate(new Vector3(0f, 0f, rng.RandfRange(-0.01f, 0.01f)));
        currentHealth = MaxHealth;
        ApplySpriteMaterial();
    }

    public async virtual void ApplyDamage(float amount)
    {
        await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
        AudioManager.Call("play_random_at", HitSounds, GlobalPosition, AudioManager.Get("BUS_WORLD"), 0.1f);
        currentHealth -= amount;
        if (currentHealth <= 0)
        {
            BreakObject();
        }
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
                var pickup = pickupScene.Instantiate<Node3D>();
                pickup.Set("item", item);

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
        Node childSprite = FindSprite();
        if (childSprite == null)
            return;

        Texture2D tex = null;

        if (childSprite is Sprite3D sprite3D)
            tex = sprite3D.Texture;
        else if (childSprite is AnimatedSprite3D animSprite)
            tex = animSprite.SpriteFrames.GetFrameTexture("idle", 0);

        if (tex == null)
            return;

        if (!MaterialCache.TryGetValue(tex, out StandardMaterial3D mat))
        {
            mat = (StandardMaterial3D)BaseMaterial.Duplicate();
            mat.AlbedoTexture = tex;
            MaterialCache[tex] = mat;
        }

        if (childSprite is Sprite3D s3d)
        {
            s3d.MaterialOverride = mat;
            s3d.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
        else if (childSprite is AnimatedSprite3D as3d)
        {
            as3d.MaterialOverride = mat;
            as3d.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
    }

    private Node FindSprite()
    {
        // If your trees/rocks use Sprite3D hierarchy, this covers it.
        return GetNodeOrNull<Node>("Sprite3D")
            ?? GetNodeOrNull<Node>("AnimatedSprite3D");
    }
}
