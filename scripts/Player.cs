using Godot;

public partial class Player : CharacterBody3D
{
    public float Speed = 6.0f;
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
    private Node hotbar;
    private Camera3D camera;

    public override void _Ready()
    {
        WorldUtils = GetNode("/root/WorldUtils");
        AudioManager = GetNode("/root/AudioManager");
        world = GetNode<World>("../../");
        sprite = GetNode<AnimatedSprite3D>("AnimatedSprite3D");
        hitCooldown = GetNode<Timer>("HitCooldown");
        hitRay = GetNode<RayCast3D>("HitRay");
        hotbar = GetNode("Hotbar");
        camera = GetNode<Camera3D>("Camera3D");

        var mat = (StandardMaterial3D)ResourceLoader.Load<Material>("res://resources/materials/WorldObjectBase.tres").Duplicate();
        mat.AlbedoTexture = sprite.SpriteFrames.GetFrameTexture("idle", 0);
        sprite.MaterialOverride = mat;
        sprite.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;

        hotbar.Connect("hotbar_changed", new Callable(this, nameof(OnHotbarChanged)));
        hotbar.Connect("selected_slot_changed", new Callable(this, nameof(OnSelectedSlotChanged)));
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
        if (equippedItem is ToolItem tool)
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

        if (hitRay.IsColliding())
        {
            Node3D target = hitRay.GetCollider() as Node3D;
            if (target != null)
                tool.UseOn(target);
        }
    }

    private void OnHitCooldownTimeout()
    {
        canSwing = true;
    }

    // inventory interaction

    public void CollectItem(Item item, int count)
    {
        hotbar.Call("add_item", item, count);
    }

    // hotbar signals

    private void OnHotbarChanged(object state)
    {
        
    }

    private void OnSelectedSlotChanged(int index)
    {
        
    }

    // input 

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp)
                hotbar.Call("select_prev");
            else if (mb.ButtonIndex == MouseButton.WheelDown)
                hotbar.Call("select_next");
        }
    }

    public override void _Input(InputEvent e)
    {
        if (Input.IsActionJustPressed("zoom_in"))
            camera.Size = Mathf.Max(camera.Size - 2, 5);

        if (Input.IsActionJustPressed("zoom_out"))
            camera.Size = Mathf.Min(camera.Size + 2, 200);
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

        if (Input.IsActionPressed("use_tool"))
            UseActiveTool();
    }
}
