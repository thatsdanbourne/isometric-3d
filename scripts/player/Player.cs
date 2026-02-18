using Godot;
using System.Collections.Generic;

public partial class Player : CharacterBody3D
{
	[Signal]
	public delegate void PlayerReadyEventHandler();

	[Signal]
	public delegate void BiomeChangedEventHandler(string newBiome);

	private static readonly StandardMaterial3D BaseMaterial =
		GD.Load<StandardMaterial3D>("res://resources/materials/WorldObjectBase.tres");

	private PackedScene cameraControllerScene =
		GD.Load<PackedScene>("res://scenes/player/CameraController.tscn");

	public float Speed = 5.0f;
	public ToolItem DefaultTool;
	private Item equippedItem;
	private Item lastEquippedItem;

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
	private FacingDir lastFacing = FacingDir.SW;

	private bool placementMode = false;
	private PlaceableItem currentPlaceable;
	private PlacementPreview placementPreview;
	private Vector2I previewTile;

	public IInteractable FocusedInteractable { get; private set; }
	public ICraftingStation FocusedStation => FocusedInteractable?.GetCapability<ICraftingStation>();

	public HUD HUD;
	public Hotbar Hotbar;
	public Inventory Inventory;

	public CameraController CameraController;

	public override void _Ready()
	{
		GameManager.Instance.SetLocalPlayer(this);

		world = GetNode<World>("/root/Game/World");
		tintOverlay = world.GetNode<BiomeTintOverlay>("BiomeTint/BiomeOverlay");
		sprite = GetNode<AnimatedSprite3D>("AnimatedSprite3D");
		hitCooldown = GetNode<Timer>("HitCooldown");
		hitRay = GetNode<RayCast3D>("HitRay");
		HUD = GetNode<HUD>("/root/Game/HUD");
		Hotbar = GetNode<Hotbar>("Hotbar");
		Inventory = GetNode<Inventory>("Inventory");
		CameraController = cameraControllerScene.Instantiate<CameraController>();
		CameraController.Player = this;
		GetNode<Display>("/root/Game/Display").SetCameraController(CameraController);

		world.GetNode<Node3D>("SubViewportContainer/SubViewport/WorldObjects")
			.CallDeferred(Node.MethodName.AddChild, CameraController);

		HUD.RefreshUI();
		Hotbar.SelectedSlotChanged += _ => UpdateEquippedItem();
		Hotbar.ContainerChanged += UpdateEquippedItem;

		// var mat = (StandardMaterial3D)BaseMaterial.Duplicate();
		// sprite.MaterialOverride = mat;

		DefaultTool = ItemRegistry.GetItem("fist") as ToolItem;

		// testing items
		InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("chest"), 1);
		InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("crafting_table"), 1);
		InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("kiln"), 1);
		InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("copper_ore"), 20);
		InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("coal"), 20);
		InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("wood"), 20);
		InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("stone"), 20);

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
		var biome = world.GetBiomeAtPos(GlobalPosition);
		if (!string.IsNullOrEmpty(biome) && biome != CurrentBiome)
			OnBiomeChanged(biome);
	}

	// tool handling   

	private void UpdateEquippedItem()
	{
		var stack = Hotbar.GetSlot(Hotbar.SelectedSlot);
		var newItem = stack?.Item ?? DefaultTool;

		if (newItem == lastEquippedItem)
			return;

		lastEquippedItem = newItem;
		equippedItem = newItem;

		UpdatePlacementState(newItem);
	}

	private ToolItem GetActiveTool()
	{
		if (equippedItem is ToolItem tool)
			return tool;

		return DefaultTool;
	}

	private void UseActiveTool()
	{
		var tool = GetActiveTool();
		if (tool == null) return;

		AudioManager.Instance.PlayVariantAt("swing_fist", GlobalPosition, 0.1f);
		// AudioManager.Call("play_random_at", tool.SwingSounds, GlobalPosition, AudioManager.Get("BUS_TOOLS"), 0.1f, -12);

		var space = GetWorld3D().DirectSpaceState;
		var swingDir = aimDirection;
		var faceDir = new Vector2(swingDir.X, swingDir.Z).Normalized();

		var facing = AnimationManager.GetFacingFromInput(faceDir);
		lastFacing = facing;
		sprite.Play("idle_" + facing.ToString().ToLower());

		foreach (var dir in GetHitArcDirections(swingDir, tool.HitArcDegress, tool.HitRayCount))
		{
			var query = PhysicsRayQueryParameters3D.Create(
				GlobalPosition,
				GlobalPosition + dir * tool.HitRange
			);

			query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

			var result = space.IntersectRay(query);
			if (result.Count == 0)
				continue;

			var collider = result["collider"].As<Node>();

			WorldObject worldObject = null;
			var current = collider;

			while (current != null)
			{
				if (current is WorldObject wo)
				{
					worldObject = wo;
					break;
				}

				current = current.GetParent();
			}

			worldObject.ObjectBroken -= OnObjectBroken;
			worldObject.ObjectBroken += OnObjectBroken;

			worldObject.ObjectHitFailed -= OnHitFailed;
			worldObject.ObjectHitFailed += OnHitFailed;

			var hitPoint = (Vector3)result["position"];
			var hitDir = (worldObject.GlobalPosition - hitPoint).Normalized();
			tool.UseOn(worldObject, hitDir);
			return;
		}
	}

	private void PlaceItem()
	{
		if (currentPlaceable == null)
			return;

		if (!world.CanPlace(previewTile, currentPlaceable))
			return;

		var result = world.PlaceItem(previewTile, currentPlaceable);
		if (result)
		{
			InventoryManager.Instance.RemoveItem(this, currentPlaceable, 1);
			UpdateEquippedItem();
		}
	}

	// event handlers
	private void OnObjectBroken(WorldObject obj)
	{
		obj.ObjectBroken -= OnObjectBroken;
		CameraController?.Shake(0.3f, 0.7f);
	}

	private void OnHitFailed(WorldObject obj)
	{
		obj.ObjectHitFailed -= OnHitFailed;
		AudioManager.Instance.PlayAt("hit_fail", obj.GlobalPosition, 0.1f);
		CameraController?.Shake(0.1f, 0.3f);
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

	public void OpenCraftingUI(ICraftingStation station)
	{
		HUD.OpenCraftingUI(station);
	}

	// input 
	public override void _UnhandledInput(InputEvent e)
	{
		if (e.IsActionPressed("toggle_inventory"))
		{
			if (HUD.IsInventoryOpen)
				HUD.CloseInventoryUI();
			else if (FocusedInteractable is IItemContainer storage)
				HUD.OpenStorageUI(storage);
			else
				HUD.OpenInventoryUI();
		}

		if (e.IsActionPressed("toggle_crafting"))
		{
			if (HUD.IsCraftingOpen)
			{
				HUD.CloseCraftingUI();
			}
			else
			{
				if (FocusedStation != null)
					HUD.OpenCraftingUI(FocusedStation);
				else
					HUD.OpenCraftingUI();
			}
		}
	}


	//Movement and tool usage

	public override void _PhysicsProcess(double delta)
	{
		var inputDir = new Vector2(
			Input.GetActionStrength("move_right") - Input.GetActionStrength("move_left"),
			Input.GetActionStrength("move_down") - Input.GetActionStrength("move_up")
		).Normalized();

		if (inputDir != Vector2.Zero)
		{
			var moveVec = new Vector3(inputDir.X, 0, inputDir.Y)
				.Rotated(Vector3.Up, Mathf.DegToRad(45));

			Velocity = moveVec * Speed;
			MoveAndSlide();

			var facing = AnimationManager.GetFacingFromInput(inputDir);
			lastFacing = facing;
			sprite.Play("run_" + facing.ToString().ToLower());
		}
		else
		{
			Velocity = Vector3.Zero;
			sprite.Play("idle_" + lastFacing.ToString().ToLower());
		}

		aimDirection = GetAimDirection();

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

		if (Input.IsActionPressed("interact"))
			switch (FocusedInteractable)
			{
				case IItemContainer storage:
					HUD.OpenStorageUI(storage);
					break;
				case ICraftingStation station:
					HUD.OpenCraftingUI(station);
					break;
			}

		if (placementMode && placementPreview != null && placementPreview.IsInsideTree())
			UpdatePlacementPreview();
		else
			UpdateFocusedObject();
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

		var mousePos = viewport.GetMousePosition();

		var rayOrigin = camera.ProjectRayOrigin(mousePos);
		var rayDir = camera.ProjectRayNormal(mousePos);

		var t = (GlobalPosition.Y - rayOrigin.Y) / rayDir.Y;
		if (t < 0) return aimDirection;

		var hitPoint = rayOrigin + rayDir * t;
		var dir = hitPoint - GlobalPosition;
		dir.Y = 0;

		if (dir.LengthSquared() < 0.01f)
			return aimDirection;

		dir = dir.Rotated(Vector3.Up, Mathf.DegToRad(-45));

		return dir.Normalized();
	}

	private IEnumerable<Vector3> GetHitArcDirections(Vector3 centerDir, float arcDegrees, int rayCount)
	{
		centerDir = centerDir.Rotated(Vector3.Up, Mathf.DegToRad(45)).Normalized();
		var halfArc = arcDegrees * 0.5f;
		var step = rayCount > 1 ? arcDegrees / (rayCount - 1) : 0f;

		for (var i = 0; i < rayCount; i++)
		{
			var angle = -halfArc + step * i;
			yield return centerDir.Rotated(Vector3.Up, Mathf.DegToRad(angle)).Normalized();
		}
	}

	// create/remove/update placement preview
	private void UpdatePlacementState(Item item)
	{
		if (item is PlaceableItem placeable)
			EnterPlacementMode(placeable);
		else
			ExitPlacementMode();
	}

	private void EnterPlacementMode(PlaceableItem placeable)
	{
		if (placementMode && currentPlaceable == placeable)
			return;

		placementMode = true;
		currentPlaceable = placeable;
		CreatePlacementPreivew();
	}

	private void ExitPlacementMode()
	{
		if (!placementMode)
			return;

		placementMode = false;
		currentPlaceable = null;
		RemovePlacementPreview();
	}

	private void CreatePlacementPreivew()
	{
		if (placementPreview != null)
			placementPreview.Free();

		placementPreview = GD.Load<PackedScene>("res://scenes/placeables/PlacementPreview.tscn")
			.Instantiate<PlacementPreview>();

		placementPreview.SetPreviewScene(currentPlaceable.PreviewScene);

		GetTree().CurrentScene.CallDeferred("add_child", placementPreview);

		var camera = GetViewport().GetCamera3D();
		if (camera == null)
			return;

		placementPreview.SetDeferred("global_position", TileManager.GetMouseTilePosition(camera));
		placementPreview.CallDeferred("force_update_transform");
	}

	private void UpdatePlacementPreview()
	{
		if (!placementMode || placementPreview == null || !placementPreview.IsInsideTree())
			return;

		if (HUD.WindowOpen)
		{
			placementPreview.Visible = false;
			return;
		}
		else
		{
			placementPreview.Visible = true;
		}


		var camera = GetViewport().GetCamera3D();
		if (camera == null)
			return;

		previewTile = TileManager.GetMouseTilePosition(camera);

		placementPreview.GlobalPosition = TileManager.TileToWorld(previewTile);
		var canPlace = world.CanPlace(previewTile, currentPlaceable);
		placementPreview.SetValid(canPlace);
	}

	private void RemovePlacementPreview()
	{
		if (placementPreview == null)
			return;

		if (placementPreview.IsInsideTree())
			placementPreview.Free();

		placementPreview = null;
	}

	private void UpdateFocusedObject()
	{
		var camera = GetViewport().GetCamera3D();
		if (camera == null)
			return;

		var mousePos = GetViewport().GetMousePosition();
		var rayOrigin = camera.ProjectRayOrigin(mousePos);
		var rayDir = camera.ProjectRayNormal(mousePos);

		var query = PhysicsRayQueryParameters3D.Create(
			rayOrigin,
			rayOrigin + rayDir * 100f
		);

		query.CollideWithAreas = false;
		query.CollideWithBodies = true;

		var result = GetWorld3D().DirectSpaceState.IntersectRay(query);

		IInteractable newFocus = null;

		if (result.Count > 0)
		{
			var collider = result["collider"].As<Node>();
			newFocus = FindAncestor<IInteractable>(collider);
		}

		if (newFocus == FocusedInteractable)
			return;

		FocusedInteractable?.OnFocusLost();
		FocusedInteractable = newFocus;
		FocusedInteractable?.OnFocusGained();
	}

	private static T FindAncestor<T>(Node node) where T : class
	{
		while (node != null)
		{
			if (node is T match)
				return match;

			node = node.GetParent();
		}

		return null;
	}
}