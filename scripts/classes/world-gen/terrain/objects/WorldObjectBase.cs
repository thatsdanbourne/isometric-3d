using Godot;

public partial class WorldObjectBase : Node3D
{
    [Signal] public delegate void ObjectBrokenEventHandler(WorldObjectBase obj);
    [Signal] public delegate void ObjectHitFailedEventHandler(WorldObjectBase obj);

    public ChunkObject Data;
    public World World;
    public ToolTier RequiredTier;
    public float MaxHealth;
    [Export] public string ObjectType { get; set; }

    public virtual void Reset() { }
    public virtual void Initialise(WorldObjectDefinition definition) { }
    public virtual void HitFailed() { }
    public virtual void ApplyDamage(float amount, Vector3 fromDirection) { }
}
