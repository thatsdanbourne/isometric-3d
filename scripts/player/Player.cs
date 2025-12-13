using Godot;

public partial class Player : CharacterBody3D
{
    [Signal] public delegate void PlayerReadyEventHandler();
    [Signal] public delegate void BiomeChangedEventHandler(string newBiome);

    private PackedScene cameraControllerScene =
        GD.Load<PackedScene>("res://scenes/player/CameraController.tscn");

    public float Speed = 5.0f;
    public ToolItem DefaultTool;
    private Item equippedItem;

    private bool canSwing = true;

    public string CurrentBiome { get; private set; } = "";
    private Vector3 _lastCheckedPosition;
    private const float BIOME_CHECK_DISTANCE = 0.5f;

    private World world;
    private BiomeTintOverlay tintOverlay;

    private AnimatedSprite3D sprite;
    private Timer hitCooldown;
    private RayCast3D hitRay;
    private Vector3 aimDirection = Vector3.Forward;

    public HUD HUD;
    public Hotbar Hotbar;
    public Inventory Inventory;

    public CameraController CameraController;

    public override void _Ready()
    {
        GameManager.Instance.SetLocalPlayer(this);

        world = GetNode<World>("../../");
        tintOverlay = world.GetNode<BiomeTintOverlay>("BiomeTint/BiomeOverlay");
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
        Hotbar.SelectedSlotChanged += _ => UpdateEquippedItem();
        Hotbar.ContainerChanged += UpdateEquippedItem;

        var mat = (StandardMaterial3D)ResourceLoader.Load<Material>("res://resources/materials/WorldObjectBase.tres").Duplicate();
        mat.AlbedoTexture = sprite.SpriteFrames.GetFrameTexture("idle", 0);
        sprite.MaterialOverride = mat;
        sprite.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;

        DefaultTool = ItemRegistry.GetItem("fist") as ToolItem;

        EmitSignal(SignalName.PlayerReady);
    }

    public override void _Process(double delta)
    {
        if (_lastCheckedPosition.DistanceSquaredTo(GlobalPosition) < BIOME_CHECK_DISTANCE * BIOME_CHECK_DISTANCE)
            return;

        _lastCheckedPosition = GlobalPosition;

        CheckBiome();
    }

    public void CheckBiome()
    {
        string biome = world.GetBiomeAtPos(GlobalPosition);
        if (!string.IsNullOrEmpty(biome) && biome != CurrentBiome)
            OnBiomeChanged(biome);
    }

    // tool handling   

    private void UpdateEquippedItem()
    {
        var stack = Hotbar.GetSlot(Hotbar.SelectedSlot);

        if (stack == null || stack.Item == null)
        {
            equippedItem = DefaultTool;
            return;
        }

        equippedItem = stack.Item;
    }

    private ToolItem GetActiveTool()
    {
        if (equippedItem is ToolItem tool)
            return tool;

        return DefaultTool;
    }

    private void UseActiveTool()
    {
        ToolItem tool = GetActiveTool();
        if (tool == null) return;

        AudioManager.Instance.PlayAt(tool.SwingSoundsKey, GlobalPosition, 0.1f);
        // AudioManager.Call("play_random_at", tool.SwingSounds, GlobalPosition, AudioManager.Get("BUS_TOOLS"), 0.1f, -12);

        if (hitRay.IsColliding() && hitRay.GetCollider() is WorldObject wo)
        {
            wo.ObjectBroken -= OnObjectBroken;
            wo.ObjectBroken += OnObjectBroken;

            Vector3 dir = (wo.GlobalPosition - hitRay.GetCollisionPoint()).Normalized();
            tool.UseOn(wo, dir);
        }
    }

    private void PlaceItem()
    {
        PlaceableItem item = equippedItem as PlaceableItem;
        WorldObject obj = item.PlaceableScene.Instantiate<WorldObject>();
        GetParent().AddChild(obj);
        obj.GlobalPosition = GlobalPosition + hitRay.TargetPosition.Normalized();
        InventoryManager.Instance.RemoveItem(this, item, 1);
        UpdateEquippedItem();
    }

    private void OnObjectBroken(WorldObject obj)
    {
        obj.ObjectBroken -= OnObjectBroken;
        CameraController?.Shake(0.3f, 0.7f);
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

        aimDirection = GetAimDirection();
        hitRay.TargetPosition = aimDirection * 2.0f;

        if (Input.IsActionPressed("use_tool") && !HUD.WindowOpen)
        {
            if (!canSwing) return;
            canSwing = false;
            hitCooldown.Start();


            if (equippedItem is PlaceableItem)
                PlaceItem();
            else
                UseActiveTool();
        }
    }


    // biome updates
    public void OnBiomeChanged(string newBiome)
    {
        CurrentBiome = newBiome;
        tintOverlay.SetTintForBiome(newBiome);
        EmitSignal(SignalName.BiomeChanged, newBiome);
    }

    // helpers
    private Vector3 GetAimDirection()
    {
        var viewport = GetViewport();
        var camera = viewport.GetCamera3D();
        if (camera == null) return aimDirection;

        Vector2 mousePos = viewport.GetMousePosition();

        Vector3 rayOrigin = camera.ProjectRayOrigin(mousePos);
        Vector3 rayDir = camera.ProjectRayNormal(mousePos);

        float t = (GlobalPosition.Y - rayOrigin.Y) / rayDir.Y;
        if (t < 0) return aimDirection;

        Vector3 hitPoint = rayOrigin + rayDir * t;
        Vector3 dir = hitPoint - GlobalPosition;
        dir.Y = 0;

        if (dir.LengthSquared() < 0.01f)
            return aimDirection;

        dir = dir.Rotated(Vector3.Up, Mathf.DegToRad(-45));

        return dir.Normalized();
    }
}
