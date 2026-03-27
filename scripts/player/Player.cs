using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class Player : CharacterBody3D, IToolHittable
{
	[Signal]
	public delegate void PlayerReadyEventHandler();

	public event Action<BiomeId> BiomeChanged;

	private static readonly PackedScene CameraControllerScene =
		GD.Load<PackedScene>("res://scenes/player/CameraController.tscn");

	public float MaxHealth = 20f;
	public float Health;
	public float Speed = 5.0f;
	public float AimLockTime = 0.5f;
	private float _aimLockTimer;
	private float _knockbackResistance = 1f;
	private float _knockbackDecay = 14f;
	private Vector3 _knockbackVelocity;
	public ToolItem DefaultTool;
	private Item _equippedItem;
	private Item _lastEquippedItem;
	private PlayerEquipment _equipment;

	private World _world;
	public BiomeId CurrentBiome = BiomeId.Unknown;
	private Vector3 _lastCheckedPosition;
	private const float BiomeCheckDistance = 0.5f;

	private BiomeTintOverlay _tintOverlay;

	private AnimationTree _animTree;
	private Vector3 _aimDirection = Vector3.Forward;
	private string _animState = "";
	private float _locomotionBlend;
	private float _locomotionBlendTarget;

	private const string LocomotionBlendPath = "parameters/Locomotion/blend_position";
	private const string PunchRequestPath = "parameters/PunchOS/request";
	private const string AxeRequestPath = "parameters/AxeOS/request";

	private float _hitCooldownAccum;
	private bool CanSwing => _hitCooldownAccum <= 0;

	private PlacementController _placement;

	public IInteractable FocusedInteractable { get; private set; }
	public ICraftingStation FocusedStation => FocusedInteractable?.GetCapability<ICraftingStation>();
	private float _focusAccum;
	private const float FocusHz = 20f;
	private const float FocusInterval = 1f / FocusHz;

	private const uint InteractableMask = 1u << 2;
	private PhysicsRayQueryParameters3D _focusQuery;
	private const uint HittableMask = 1u << 1;
	private PhysicsRayQueryParameters3D _toolQuery;

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
		_tintOverlay = _world.GetNode<BiomeTintOverlay>("BiomeTint/BiomeOverlay");
		HUD = GetNode<HUD>("/root/Game/HUD");
		Hotbar = GetNode<Hotbar>("Hotbar");
		Inventory = GetNode<Inventory>("Inventory");
		_equipment = GetNode<PlayerEquipment>("PlayerEquipment");

		CameraController = CameraControllerScene.Instantiate<CameraController>();
		CameraController.Player = this;

		_world.GetNode<Node3D>("WorldObjects")
			.CallDeferred(Node.MethodName.AddChild, CameraController);

		HUD.RefreshUI();
		Hotbar.SelectedSlotChanged += _ => UpdateEquippedItem();
		Hotbar.ContainerChanged += UpdateEquippedItem;

		DefaultTool = ItemRegistry.GetItem("fist") as ToolItem;
		Health = MaxHealth;

		_placement = new PlacementController();
		AddChild(_placement);
		_placement.Init(_world, this);

		_focusQuery = new PhysicsRayQueryParameters3D
		{
			CollideWithAreas = false,
			CollideWithBodies = true,
			CollisionMask = InteractableMask
		};

		_toolQuery = new PhysicsRayQueryParameters3D
		{
			CollideWithAreas = false,
			CollideWithBodies = true,
			CollisionMask = HittableMask
		};


#if DEBUG
		// testing items
		InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("stone_sword"), 1);
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

		if (biome != CurrentBiome)
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

		if (newItem is ToolItem { HeldItemScene: not null } tool)
			_equipment.EquipTool(tool.HeldItemScene);
		else
			_equipment.UnequipTool();

		UpdatePlacementState(newItem);
	}

	private ToolItem GetActiveTool()
	{
		if (_equippedItem is ToolItem tool)
			return tool;

		return DefaultTool;
	}

	private async Task UseActiveTool()
	{
		if (!CanSwing) return;

		var tool = GetActiveTool();
		if (tool == null) return;

		_hitCooldownAccum = tool.CooldownSeconds;

		switch (tool.ToolType)
		{
			case "axe":
			case "sword":
				_animTree.Set(AxeRequestPath, (int)AnimationNodeOneShot.OneShotRequest.Fire);
				break;
			default:
				_animTree.Set(PunchRequestPath, (int)AnimationNodeOneShot.OneShotRequest.Fire);
				break;
		}


		AudioManager.Instance.PlayVariantAt("swing_fist", GlobalPosition, 0.1f);

		var space = GetWorld3D().DirectSpaceState;
		var swingDir = _aimLockTimer > AimLockTime
			? Velocity.Normalized()
			: _aimDirection.Rotated(Vector3.Up, Mathf.DegToRad(45)).Normalized();

		var targetAngle = Mathf.Atan2(swingDir.X, swingDir.Z);

		Rotation = new Vector3(
			Rotation.X,
			targetAngle,
			Rotation.Z
		);

		await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);

		var hitResult = CombatUtils.PerformMeleeHit(this, tool, swingDir, space, _toolQuery);

		switch (hitResult.Outcome)
		{
			case ToolHitOutcome.Failed:
				OnHitFailed();
				break;
			case ToolHitOutcome.Destroyed:
				OnObjectBroken();
				break;
		}
	}

	private void OnObjectBroken()
	{
		CameraController?.Shake(0.3f, 0.7f);
	}

	private void OnHitFailed()
	{
		CameraController?.Shake(0.1f, 0.3f);
	}

	// inventory interaction
	public void CollectItem(Item item, int count)
	{
		InventoryManager.Instance.AddItem(this, item, count);
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

		if (_hitCooldownAccum > 0)
			_hitCooldownAccum -= dt;

		var inputDir = new Vector2(
			Input.GetActionStrength("move_right") - Input.GetActionStrength("move_left"),
			Input.GetActionStrength("move_down") - Input.GetActionStrength("move_up")
		).Normalized();

		var moveVelocity = Vector3.Zero;

		if (inputDir != Vector2.Zero)
		{
			_aimLockTimer += dt;

			var moveVec = new Vector3(inputDir.X, 0, inputDir.Y)
				.Rotated(Vector3.Up, Mathf.DegToRad(45));

			moveVelocity = moveVec * Speed;

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
			_aimLockTimer = 0f;
			SetAnimState("idle");
		}

		_knockbackVelocity = _knockbackVelocity.MoveToward(Vector3.Zero, _knockbackDecay * dt);
		_knockbackVelocity.Y = 0f;

		Velocity = new Vector3(moveVelocity.X + _knockbackVelocity.X, 0, moveVelocity.Z + _knockbackVelocity.Z);

		if (_knockbackVelocity > Vector3.Zero) GD.Print("moving");

		MoveAndSlide();

		const float k = 14f;
		var a = 1f - Mathf.Exp(-k * dt);

		_locomotionBlend = Mathf.Lerp(_locomotionBlend, _locomotionBlendTarget, a);
		_animTree.Set(LocomotionBlendPath, _locomotionBlend);

		if (camera != null && !hudOpen)
			_aimDirection = GetAimDirection(camera, viewport.GetMousePosition());


		if (useToolHeld && !hudOpen)
			if (_equippedItem is PlaceableItem)
			{
				if (_placement.TryPlace()) UpdateEquippedItem();
			}
			else
			{
				_ = UseActiveTool();
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

		_locomotionBlendTarget = name switch
		{
			"idle" => 0f,
			"run" => 1f,
			_ => _locomotionBlendTarget
		};
	}

	// biome updates
	public void OnBiomeChanged(BiomeId newBiome)
	{
		CurrentBiome = newBiome;
		_tintOverlay.SetTintForBiome(newBiome);
		BiomeChanged?.Invoke(newBiome);
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
			var body = col.As<Node>() as WorldObjectCollider;
			newFocus = body?.ObjectOwner as IInteractable;
		}

		if (newFocus == FocusedInteractable)
			return;

		FocusedInteractable?.OnFocusLost();
		FocusedInteractable = newFocus;
		FocusedInteractable?.OnFocusGained();
	}

	public Node3D GetHitRoot()
	{
		return this;
	}

	public ToolHitOutcome ReceiveToolHit(ToolItem tool, float damage, Vector3 fromDirection, Vector3 hitPoint)
	{
		ApplyKnockback(fromDirection, 5f);
		Health -= damage;
		if (Health <= 0)
		{
			GD.Print("bro is dead");
			return ToolHitOutcome.Destroyed;
		}

		return ToolHitOutcome.Hit;
	}

	private void ApplyKnockback(Vector3 direction, float strength)
	{
		direction.Y = 0;

		if (direction.LengthSquared() < 0.001f) return;

		_knockbackVelocity += direction.Normalized() * (strength / _knockbackResistance);
	}

	public ToolHitOutcome ReceiveToolHitFailed(ToolItem tool, Vector3 fromDirection, Vector3 hitPoint)
	{
		return ToolHitOutcome.Failed;
	}
}