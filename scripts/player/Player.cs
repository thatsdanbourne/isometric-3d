using Godot;

public partial class Player : CharacterBody3D
{
    [Signal] public delegate void PlayerReadyEventHandler();

    private PackedScene cameraControllerScene = 
        GD.Load<PackedScene>("res://scenes/player/CameraController.tscn");

    public float Speed = 5.0f;
    public ToolItem DefaultTool = ResourceLoader.Load<ToolItem>("res://items/tools/fist/Fist.tres");
    private Item equippedItem;

    private bool canSwing = true;

    public string CurrentBiome { get; private set; } = "";

    private World world;
    private GodotObject WorldUtils;
    private GodotObject AudioManager;

    private AnimatedSprite3D sprite;
    private Timer hitCooldown;
    private RayCast3D hitRay;

    public HUD HUD;
    public Hotbar Hotbar;
    public Inventory Inventory;

    public CameraController CameraController;

    public override void _Ready()
    {
        WorldUtils = GetNode("/root/WorldUtils");
        AudioManager = GetNode("/root/AudioManager");
        world = GetNode<World>("../../");
        sprite = GetNode<AnimatedSprite3D>("AnimatedSprite3D");
        hitCooldown = GetNode<Timer>("HitCooldown");
        hitRay = GetNode<RayCast3D>("HitRay");
        HUD = GetNode<HUD>("/root/Game/HUD");
        Hotbar = GetNode<Hotbar>("Hotbar");
        Inventory = GetNode<Inventory>("Inventory");
        CameraController = cameraControllerScene.Instantiate<CameraController>();
        CameraController.Player = this;

        world.CallDeferred(Node.MethodName.AddChild, CameraController);

        HUD.RefreshUI();

        var mat = (StandardMaterial3D)ResourceLoader.Load<Material>("res://resources/materials/WorldObjectBase.tres").Duplicate();
        mat.AlbedoTexture = sprite.SpriteFrames.GetFrameTexture("idle", 0);
        sprite.MaterialOverride = mat;
        sprite.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;

        EmitSignal(SignalName.PlayerReady);
    }

    public override void _Process(double delta)
    {
        string biome = world.GetBiomeAtPos(GlobalPosition);
        if (!string.IsNullOrEmpty(biome) && biome != CurrentBiome)
        {
            CurrentBiome = biome;
            WorldUtils.Call("update_biome_tint", CurrentBiome);
        }
    }

    // tool handling   

    private ToolItem GetActiveTool()
    {
        var stack = Hotbar.GetSlot(Hotbar.SelectedSlot);
        if (stack != null && stack.Item is ToolItem tool)
            return tool;
        
        return DefaultTool;
    }

    private void UseActiveTool()
    {
        if (!canSwing) return;
        canSwing = false;

        ToolItem tool = GetActiveTool();
        if (tool == null) return;

        AudioManager.Call("play_random_at", tool.SwingSounds, GlobalPosition, AudioManager.Get("BUS_TOOLS"), 0.1f, -12);

        hitCooldown.Start();

        if (hitRay.IsColliding() && hitRay.GetCollider() is WorldObject wo)
        {
            wo.ObjectBroken -= OnObjectBroken;
            wo.ObjectBroken += OnObjectBroken;

            Vector3 dir = (wo.GlobalPosition - hitRay.GetCollisionPoint()).Normalized();
            tool.UseOn(wo, dir);
        }
    }

    private void OnObjectBroken(WorldObject obj)
    {
        obj.ObjectBroken -= OnObjectBroken;
        CameraController?.Shake(0.3f, 1f);
    }

    private void OnHitCooldownTimeout()
    {
        canSwing = true;
    }

    // inventory interaction

    public void CollectItem(Item item, int count)
    {
        InventoryManager.Instance.AddItem(this, item, count);
    }

    // input 

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp)
                Hotbar.SelectPrev();
            else if (mb.ButtonIndex == MouseButton.WheelDown)
                Hotbar.SelectNext();
        }
    }

    //Movement and tool usage

    public override void _PhysicsProcess(double delta)
    {
        Vector2 inputDir = new Vector2(
            Input.GetActionStrength("move_right") - Input.GetActionStrength("move_left"),
            Input.GetActionStrength("move_down") - Input.GetActionStrength("move_up")
        ).Normalized();

        if (inputDir != Vector2.Zero)
        {
            hitRay.TargetPosition = new Vector3(inputDir.X, 0, inputDir.Y) * 2f;

            Vector3 moveVec = new Vector3(inputDir.X, 0, inputDir.Y)
                .Rotated(Vector3.Up, Mathf.DegToRad(45));

            Velocity = moveVec * Speed;
            MoveAndSlide();
        }
        else
        {
            Velocity = Vector3.Zero;
            sprite.Play("idle");
        }

        if (Input.IsActionPressed("use_tool") && !HUD.WindowOpen)
            UseActiveTool();
    }
}
