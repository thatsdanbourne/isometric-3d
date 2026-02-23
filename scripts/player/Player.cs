using System.Collections.Generic;
using Godot;

public partial class Player : CharacterBody3D
{
	[Signal]
	public delegate void PlayerReadyEventHandler();

	[Signal]
	public delegate void BiomeChangedEventHandler(string newBiome);

	private static readonly PackedScene CameraControllerScene =
		GD.Load<PackedScene>("res://scenes/player/CameraController.tscn");

	public float Speed = 5.0f;
	public ToolItem DefaultTool;
	private Item _equippedItem;
	private Item _lastEquippedItem;

	private bool _canSwing = true;

	private World _world;
	public string CurrentBiome { get; private set; } = "";
	private Vector3 _lastCheckedPosition;
	private const float BiomeCheckDistance = 0.5f;

	private BiomeTintOverlay _tintOverlay;

	private AnimationTree _animTree;
	private Vector3 _aimDirection = Vector3.Forward;
	private string _animState = "";
	private AnimationNodeStateMachinePlayback _animPlayback;
	private float _locomotionBlend;
	private float _locomotionBlendTarget;
	private const string LocomotionBlendPath = "parameters/Locomotion/IdleRun/blend_position";

	private Timer _hitCooldown;

	private PlacementController _placement;

	public IInteractable FocusedInteractable { get; private set; }
	public ICraftingStation FocusedStation => FocusedInteractable?.GetCapability<ICraftingStation>();
	private float _focusAccum;
	private const float FocusHz = 20f;
	private const float FocusInterval = 1f / FocusHz;
	private PhysicsRayQueryParameters3D _focusQuery;

	public HUD HUD;
	public Hotbar Hotbar;
	public Inventory Inventory;

	public CameraController CameraController;

	private static readonly StringName UseToolAction = "use_tool";
	private static readonly StringName InteractAction = "interact";

	public override void _Ready()
	{
		GameManager.Instance.SetLocalPlayer(this);

		_world = GetNode<World>("/root/Game/World");
		_animTree = GetNode<AnimationTree>("AnimationTree");
		_animPlayback = _animTree.Get("parameters/playback").As<AnimationNodeStateMachinePlayback>();
		_animPlayback?.Travel("Locomotion");
		_tintOverlay = _world.GetNode<BiomeTintOverlay>("BiomeTint/BiomeOverlay");
		_hitCooldown = GetNode<Timer>("HitCooldown");
		HUD = GetNode<HUD>("/root/Game/HUD");
		Hotbar = GetNode<Hotbar>("Hotbar");
		Inventory = GetNode<Inventory>("Inventory");
		CameraController = CameraControllerScene.Instantiate<CameraController>();
		CameraController.Player = this;
		GetNode<Display>("/root/Game/Display").SetCameraController(CameraController);

		_world.GetNode<Node3D>("SubViewportContainer/SubViewport/WorldObjects")
			.CallDeferred(Node.MethodName.AddChild, CameraController);

		HUD.RefreshUI();
		Hotbar.SelectedSlotChanged += _ => UpdateEquippedItem();
		Hotbar.ContainerChanged += UpdateEquippedItem;

		DefaultTool = ItemRegistry.GetItem("fist") as ToolItem;

		_placement = new PlacementController();
		AddChild(_placement);
		_placement.Init(_world, this);

		_focusQuery = new PhysicsRayQueryParameters3D
		{
			CollideWithAreas = false,
			CollideWithBodies = true
		};

#if DEBUG
		// testing items
		InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("chest"), 1);
		InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("crafting_table"), 1);
		InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("kiln"), 1);
		InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("copper_ore"), 20);
		InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("coal"), 20);
		InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("wood"), 20);
		InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("stone"), 20);
#endif

		EmitSignal(SignalName.PlayerReady);
	}

	public override void _Process(double delta)
	{
		if (_lastCheckedPosition.DistanceSquaredTo(GlobalPosition) < BiomeCheckDistance * BiomeCheckDistance)
			return;

		_lastCheckedPosition = GlobalPosition;
		CheckBiome();
	}

	public void CheckBiome()
	{
		var biome = _world.GetBiomeAtPos(GlobalPosition);
		if (!string.IsNullOrEmpty(biome) && biome != CurrentBiome)
			OnBiomeChanged(biome);
	}

	// tool handling   

	private void UpdateEquippedItem()
	{
		var stack = Hotbar.GetSlot(Hotbar.SelectedSlot);
		var newItem = stack?.Item ?? DefaultTool;

		if (newItem == _lastEquippedItem)
			return;

		_lastEquippedItem = newItem;
		_equippedItem = newItem;

		UpdatePlacementState(newItem);
	}

	private ToolItem GetActiveTool()
	{
		if (_equippedItem is ToolItem tool)
			return tool;

		return DefaultTool;
	}

	private void UseActiveTool()
	{
		var tool = GetActiveTool();
		if (tool == null) return;

		AudioManager.Instance.PlayVariantAt("swing_fist", GlobalPosition, 0.1f);

		var space = GetWorld3D().DirectSpaceState;
		var swingDir = _aimDirection;

		foreach (var dir in GetHitArcDirections(swingDir, tool.HitArcDegress, tool.HitRayCount))
		{
			var query = PhysicsRayQueryParameters3D.Create(
				GlobalPosition,
				GlobalPosition + dir * tool.HitRange
			);

			query.Exclude = [GetRid()];

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

			if (worldObject == null) continue;

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
		_canSwing = true;
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
		var dt = (float)delta;

		var hudOpen = HUD.WindowOpen;
		var useToolHeld = Input.IsActionPressed(UseToolAction);
		var interactPressed = Input.IsActionJustPressed(InteractAction);

		var viewport = GetViewport();
		var camera = viewport.GetCamera3D();

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

			var targetAngle = Mathf.Atan2(moveVec.X, moveVec.Z);

			Rotation = new Vector3(
				Rotation.X,
				Mathf.LerpAngle(Rotation.Y, targetAngle, 10f * dt),
				Rotation.Z
			);

			SetAnimState("run");
		}
		else
		{
			Velocity = Vector3.Zero;
			SetAnimState("idle");
		}

		const float k = 14f;
		var a = 1f - Mathf.Exp(-k * dt);

		_locomotionBlend = Mathf.Lerp(_locomotionBlend, _locomotionBlendTarget, a);
		_animTree.Set(LocomotionBlendPath, _locomotionBlend);

		if (camera != null && !hudOpen)
			_aimDirection = GetAimDirection(camera, viewport.GetMousePosition());

		if (useToolHeld && !hudOpen)
			if (_canSwing)
			{
				_canSwing = false;
				_hitCooldown.Start();

				if (_equippedItem is PlaceableItem)
				{
					if (_placement.TryPlace()) UpdateEquippedItem();
				}
				else
				{
					UseActiveTool();
				}
			}

		if (interactPressed && !hudOpen)
			switch (FocusedInteractable)
			{
				case IItemContainer storage:
					HUD.OpenStorageUI(storage);
					break;
				case ICraftingStation station:
					HUD.OpenCraftingUI(station);
					break;
			}

		if (_placement.Active)
		{
			if (camera != null)
				_placement.Tick(camera, !hudOpen);
		}
		else if (!hudOpen)
		{
			_focusAccum += dt;
			if (!(_focusAccum >= FocusInterval)) return;

			_focusAccum -= FocusInterval;
			if (camera != null) UpdateFocusedObject(camera, viewport.GetMousePosition());
		}
	}

	private void SetAnimState(string name)
	{
		if (_animState == name) return;
		_animState = name;

		_animPlayback?.Travel("Locomotion");
		_locomotionBlendTarget = name switch
		{
			"idle" => 0f,
			"run" => 1f,
			_ => _locomotionBlendTarget
		};
	}

	// biome updates
	public void OnBiomeChanged(string newBiome)
	{
		CurrentBiome = newBiome;
		_tintOverlay.SetTintForBiome(newBiome);
		EmitSignal(SignalName.BiomeChanged, newBiome);
	}

	// helpers
	private Vector3 GetAimDirection(Camera3D camera, Vector2 mousePos)
	{
		if (camera == null) return _aimDirection;

		var rayOrigin = camera.ProjectRayOrigin(mousePos);
		var rayDir = camera.ProjectRayNormal(mousePos);

		if (Mathf.Abs(rayDir.Y) < 0.0001f) return _aimDirection;

		var t = (GlobalPosition.Y - rayOrigin.Y) / rayDir.Y;
		if (t < 0) return _aimDirection;

		var hitPoint = rayOrigin + rayDir * t;
		var dir = hitPoint - GlobalPosition;
		dir.Y = 0;

		if (dir.LengthSquared() < 0.01f)
			return _aimDirection;

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

	private void UpdatePlacementState(Item item)
	{
		if (item is PlaceableItem placeable)
			_placement.Enter(placeable);
		else
			_placement.Exit();
	}

	private void UpdateFocusedObject(Camera3D camera, Vector2 mousePos)
	{
		if (camera == null)
			return;

		var rayOrigin = camera.ProjectRayOrigin(mousePos);
		var rayDir = camera.ProjectRayNormal(mousePos);

		_focusQuery.From = rayOrigin;
		_focusQuery.To = rayOrigin + rayDir * 100f;

		var result = GetWorld3D().DirectSpaceState.IntersectRay(_focusQuery);

		IInteractable newFocus = null;

		if (result.Count > 0 && result.TryGetValue("collider", out var col))
		{
			var collider = col.As<Node>();
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