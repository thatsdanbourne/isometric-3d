using System;
using System.Threading.Tasks;
using Godot;
using Godot.NativeInterop;

public partial class Player : CharacterBody3D, IToolHittable
{
	[Signal]
	public delegate void PlayerReadyEventHandler();

	public event Action<BiomeId> BiomeChanged;

	private static readonly PackedScene CameraControllerScene =
		GD.Load<PackedScene>("res://scenes/entities/player/CameraController.tscn");

	private static readonly StringName UseToolAction = "use_tool";
	private static readonly StringName InteractAction = "interact";

	private const float BiomeCheckDistance = 0.5f;
	private const float FocusHz = 20f;
	private const float FocusInterval = 1f / FocusHz;
	private const uint InteractableMask = 1u << 2;
	private const uint HittableMask = 1u << 1;

	private const string LocomotionBlendPath = "parameters/Locomotion/blend_position";
	private const string PunchRequestPath = "parameters/PunchOS/request";
	private const string AxeRequestPath = "parameters/AxeOS/request";

	public int PlayerId { get; set; }
	public bool IsLocal { get; set; }

	public float MaxHealth = 20f;
	public float Health;
	public float Speed = 5.0f;
	public float AimLockTime = 0.5f;
	public ToolItem DefaultTool;
	public BiomeId CurrentBiome = BiomeId.Unknown;

	public float Gravity = 24f;
	private float _verticalVelocity;

	public IInteractable FocusedInteractable { get; private set; }

	public HUD HUD;
	public Hotbar Hotbar;
	public Inventory Inventory;
	public ItemStack DraggedStack;
	public CameraController CameraController;
	private Label3D _nameplate;

	private float _aimLockTimer;
	private float _footstepTimer;
	private float _footstepInterval = 0.4f;
	private float _knockbackResistance = 1f;
	private float _knockbackDecay = 14f;
	public Vector3 KnockbackVelocity;
	private float _hitCooldownAccum;
	private float _focusAccum;
	private float _locomotionBlend;
	private float _locomotionBlendTarget;

	private Vector3 _lastCheckedPosition;
	private Vector3 _aimDirection = Vector3.Forward;
	private string _animState = "";

	private bool _testItemsGiven;
	private bool _suppressSelectedSlotRequest;

	private Item _equippedItem;
	private Item _lastEquippedItem;
	private PlayerEquipment _equipment;
	private PlacementController _placement;

	private World _world;
	private BiomeTintOverlay _tintOverlay;
	private AnimationTree _animTree;
	private PhysicsRayQueryParameters3D _focusQuery;
	public PhysicsRayQueryParameters3D ToolQuery;

	public bool CanSwing => _hitCooldownAccum <= 0;

	// lifecycle
	public override void _Ready()
	{
		Health = MaxHealth;
		DefaultTool = ItemRegistry.GetItem("fist") as ToolItem;

		_world = GameManager.Instance.CurrentWorld;
		_animTree = GetNode<AnimationTree>("AnimationTree");
		_tintOverlay = _world.GetNode<BiomeTintOverlay>("BiomeTint/BiomeOverlay");
		_equipment = GetNode<PlayerEquipment>("PlayerEquipment");
		Hotbar = GetNode<Hotbar>("Hotbar");
		Hotbar.SelectedSlotChanged += OnSelectedSlotChanged;
		Hotbar.ContainerChanged += OnHotbarContainerChanged;
		Inventory = GetNode<Inventory>("Inventory");
		_nameplate = GetNode<Label3D>("Nameplate");
		_nameplate.Visible = !IsLocal;
		_nameplate.Text = $"Player {PlayerId}";

		_focusQuery = new PhysicsRayQueryParameters3D
		{
			CollideWithAreas = false,
			CollideWithBodies = true,
			CollisionMask = InteractableMask
		};

		ToolQuery = new PhysicsRayQueryParameters3D
		{
			CollideWithAreas = false,
			CollideWithBodies = true,
			CollisionMask = HittableMask
		};

		if (!Multiplayer.HasMultiplayerPeer() || (Multiplayer.IsServer() && !_testItemsGiven))
		{
			_testItemsGiven = true;

			// testing items
			InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("stone_sword"), 1);
			// InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("chest"), 1);
			// InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("crafting_table"), 1);
			// InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("kiln"), 1);
			// InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("copper_ore"), 99);
			// InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("coal"), 99);
			// InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("wood"), 99);
			InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("campfire"), 2);

			if (Multiplayer.HasMultiplayerPeer() && Multiplayer.IsServer())
				_world.Sync.SyncPlayerInventoryState(this);
		}

		if (!IsLocal)
			return;

		CameraController = CameraControllerScene.Instantiate<CameraController>();
		CameraController.Player = this;
		AddChild(CameraController);

		HUD = GetNode<HUD>("/root/Bootstrap/ClientUI/HUD");
		HUD.RefreshUI();

		_placement = new PlacementController();
		AddChild(_placement);
		_placement.Init(_world, this);

		GetNode<AudioListener3D>("AudioListener3D").MakeCurrent();
		EmitSignal(SignalName.PlayerReady);
	}

	public override void _Process(double delta)
	{
		if (_lastCheckedPosition.DistanceSquaredTo(GlobalPosition) < BiomeCheckDistance * BiomeCheckDistance)
			return;

		_lastCheckedPosition = GlobalPosition;
		CheckBiome();
	}

	public override void _PhysicsProcess(double delta)
	{
		var dt = (float)delta;

		_footstepTimer -= dt;
		var isMoving = Velocity.LengthSquared() > 0.01f;
		if (isMoving)
			TryPlayFootstep();

		var blendAlpha = 1f - Mathf.Exp(-14f * dt);

		if (!IsLocal)
		{
			UpdateRemotePlayer(dt, blendAlpha, isMoving);
			return;
		}

		var viewport = GetViewport();
		var camera = viewport.GetCamera3D();
		var hudOpen = HUD.WindowOpen;

		UpdateToolCooldown(dt);
		HandleLocalMovement(dt);
		UpdateLocomotionBlend(blendAlpha);
		UpdateAimDirection(camera, viewport, hudOpen);
		HandleToolUse(hudOpen);
		HandlePlaceItem(hudOpen);
		HandleInteraction(hudOpen);
		UpdatePlacementOrFocus(dt, camera, viewport, hudOpen);
		SyncTransform();
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (!IsLocal)
			return;

		if (e.IsActionPressed("toggle_inventory"))
			ToggleInventoryUI();

		if (e.IsActionPressed("toggle_crafting"))
			ToggleCraftingUI();
	}

	// input and local gameplay
	private void ToggleInventoryUI()
	{
		if (HUD.IsInventoryOpen)
		{
			HUD.CloseInventoryUI();
			return;
		}

		if (FocusedInteractable is IItemContainer storage)
		{
			HUD.OpenStorageUI(storage);
			return;
		}

		HUD.OpenInventoryUI();
	}

	private void ToggleCraftingUI()
	{
		if (HUD.IsCraftingOpen)
			HUD.CloseCraftingUI();
		else
			HUD.OpenCraftingUI();
	}

	private void HandleInteraction(bool hudOpen)
	{
		if (hudOpen || !Input.IsActionJustPressed(InteractAction))
			return;

		if (FocusedInteractable != null && FocusedInteractable.CanInteract(this))
			FocusedInteractable.Interact(this);
	}

	// movement and animation
	private void UpdateRemotePlayer(float dt, float blendAlpha, bool isMoving)
	{
		SetAnimState(isMoving ? "run" : "idle");
		UpdateLocomotionBlend(blendAlpha);
		UpdateVelocityWithKnockback(Velocity, dt);
		ApplyGravity(dt);
		MoveAndSlide();
	}

	private void HandleLocalMovement(float dt)
	{
		var inputDir = GetMovementInput();
		var moveVelocity = Vector3.Zero;

		if (inputDir != Vector2.Zero)
		{
			moveVelocity = HandleMovementInput(inputDir, dt);
			SetAnimState("run");
		}
		else
		{
			_aimLockTimer = 0f;
			SetAnimState("idle");
		}

		UpdateVelocityWithKnockback(moveVelocity, dt);
		ApplyGravity(dt);
		MoveAndSlide();
	}

	private Vector2 GetMovementInput()
	{
		return new Vector2(
			Input.GetActionStrength("move_right") - Input.GetActionStrength("move_left"),
			Input.GetActionStrength("move_down") - Input.GetActionStrength("move_up")
		).Normalized();
	}

	private Vector3 HandleMovementInput(Vector2 inputDir, float dt)
	{
		_aimLockTimer += dt;

		var moveVec = new Vector3(inputDir.X, 0, inputDir.Y).Rotated(Vector3.Up, Mathf.DegToRad(45));
		var targetAngle = Mathf.Atan2(moveVec.X, moveVec.Z);

		Rotation = new Vector3(
			Rotation.X,
			Mathf.LerpAngle(Rotation.Y, targetAngle, 10f * dt),
			Rotation.Z
		);

		return moveVec * Speed;
	}

	private void TryPlayFootstep()
	{
		if (_footstepTimer > 0f)
			return;

		var tile = _world.GetTileAtPos(GlobalPosition);
		if (tile != null)
		{
			var key = tile.Value.Definition.Id switch
			{
				TileId.Grass => "footstep_grass",
				TileId.Sand => "footstep_sand",
				TileId.Snow => "footstep_snow",
				_ => "footstep_grass"
			};

			AudioManager.Instance.PlayVariantAt(key, GlobalPosition, AudioManager.BusFootsteps, 0.2f);
		}

		_footstepTimer = _footstepInterval;
	}

	private void UpdateVelocityWithKnockback(Vector3 moveVelocity, float dt)
	{
		KnockbackVelocity = KnockbackVelocity.MoveToward(Vector3.Zero, _knockbackDecay * dt);
		Velocity = new Vector3(moveVelocity.X + KnockbackVelocity.X, moveVelocity.Y + KnockbackVelocity.Y,
			moveVelocity.Z + KnockbackVelocity.Z);
	}

	private void ApplyGravity(float dt)
	{
		const float groundY = 0f;

		if (GlobalPosition.Y > groundY + 0.02f)
		{
			_verticalVelocity -= Gravity * dt;
		}
		else
		{
			_verticalVelocity = 0f;

			var pos = GlobalPosition;
			pos.Y = groundY;
			GlobalPosition = pos;
		}

		Velocity = new Vector3(Velocity.X, _verticalVelocity, Velocity.Z);
	}

	private void SetAnimState(string name)
	{
		if (_animState == name)
			return;

		_animState = name;
		_locomotionBlendTarget = name switch
		{
			"idle" => 0f,
			"run" => 1f,
			_ => _locomotionBlendTarget
		};
	}

	private void UpdateLocomotionBlend(float blendAlpha)
	{
		_locomotionBlend = Mathf.Lerp(_locomotionBlend, _locomotionBlendTarget, blendAlpha);
		_animTree.Set(LocomotionBlendPath, _locomotionBlend);
	}

	// aiming, focus, placement
	private void UpdateAimDirection(Camera3D camera, Viewport viewport, bool hudOpen)
	{
		if (camera != null && !hudOpen)
			_aimDirection = GetAimDirection(camera, viewport.GetMousePosition());
	}

	private Vector3 GetAimDirection(Camera3D camera, Vector2 mousePos)
	{
		if (camera == null)
			return _aimDirection;

		var rayOrigin = camera.ProjectRayOrigin(mousePos);
		var rayDir = camera.ProjectRayNormal(mousePos);

		if (Mathf.Abs(rayDir.Y) < 0.0001f)
			return _aimDirection;

		var t = (GlobalPosition.Y - rayOrigin.Y) / rayDir.Y;
		if (t < 0)
			return _aimDirection;

		var hitPoint = rayOrigin + rayDir * t;
		var dir = hitPoint - GlobalPosition;
		dir.Y = 0;

		if (dir.LengthSquared() < 0.01f)
			return _aimDirection;

		dir = dir.Rotated(Vector3.Up, Mathf.DegToRad(-45));
		return dir.Normalized();
	}

	private void UpdatePlacementOrFocus(float dt, Camera3D camera, Viewport viewport, bool hudOpen)
	{
		if (_placement.Active)
		{
			if (camera != null)
				_placement.Tick(camera, !hudOpen);
			return;
		}

		if (hudOpen || camera == null)
			return;

		_focusAccum += dt;
		if (_focusAccum < FocusInterval)
			return;

		_focusAccum -= FocusInterval;
		UpdateFocusedObject(camera, viewport.GetMousePosition());
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
			var node = col.As<Node>();
			newFocus = FindInteractable(node);
		}

		if (newFocus == FocusedInteractable)
		{
			FocusedInteractable?.UpdateFocus(this);
			return;
		}

		FocusedInteractable?.OnFocusLost(this);
		FocusedInteractable = newFocus;
		FocusedInteractable?.OnFocusGained(this);
		FocusedInteractable?.UpdateFocus(this);
	}

	private void UpdatePlacementState(Item item)
	{
		if (_placement == null)
			return;

		if (item is PlaceableItem placeable)
			_placement.Enter(placeable);
		else
			_placement.Exit();
	}

	// equipped item and inventory sync
	private void UpdateEquippedItem()
	{
		var stack = Hotbar.GetSlot(Hotbar.SelectedSlot);
		var newItem = stack?.Item ?? DefaultTool;

		if (newItem == _lastEquippedItem)
			return;

		_lastEquippedItem = newItem;
		_equippedItem = newItem;

		ApplyHeldItemVisual(newItem);

		if (IsLocal)
			UpdatePlacementState(newItem);
	}

	private void ApplyHeldItemVisual(Item item)
	{
		if (item is ToolItem { HeldItemScene: not null } tool)
			_equipment.EquipTool(tool.HeldItemScene);
		else
			_equipment.UnequipTool();
	}

	private void OnSelectedSlotChanged(int slotIndex)
	{
		UpdateEquippedItem();

		if (IsLocal && !_suppressSelectedSlotRequest)
			RequestSelectedSlotSync(slotIndex);
	}

	private void OnHotbarContainerChanged()
	{
		UpdateEquippedItem();
		BroadcastHeldItem();
	}

	public void HandleSelectedSlotChanged(int slotIndex)
	{
		if (slotIndex < 0 || slotIndex >= Hotbar.SlotCount)
			return;

		_suppressSelectedSlotRequest = true;
		Hotbar.SelectSlot(slotIndex);
		_suppressSelectedSlotRequest = false;

		_world.Sync.Rpc(nameof(WorldSync.SyncSelectedHotbarSlot), PlayerId, slotIndex);
		BroadcastHeldItem();
	}

	public void ApplyRemoteSelectedSlot(int slotIndex)
	{
		if (slotIndex < 0 || slotIndex >= Hotbar.SlotCount)
			return;

		_suppressSelectedSlotRequest = true;
		Hotbar.SelectSlot(slotIndex);
		_suppressSelectedSlotRequest = false;
	}

	private void RequestSelectedSlotSync(int slotIndex)
	{
		_world.Sync.RpcId(1, nameof(WorldSync.RequestSelectHotbarSlot), slotIndex);
	}

	public void ApplyRemoteHeldItem(int slotIndex, string itemId)
	{
		if (slotIndex >= 0 && slotIndex < Hotbar.SlotCount)
			Hotbar.SelectedSlot = slotIndex;

		var item = ItemRegistry.GetItem(itemId) ?? DefaultTool;
		ApplyHeldItemVisual(item);
	}

	private void BroadcastHeldItem()
	{
		if (_world == null || !_world.Multiplayer.IsServer())
			return;

		_world.Sync.Rpc(nameof(WorldSync.SyncHeldItem), PlayerId, Hotbar.SelectedSlot, GetActiveTool().Id);
	}

	public void RequestItemPickup(ulong pickupId)
	{
		_world.Sync.RpcId(1, nameof(WorldSync.RequestPickup), pickupId);
	}

	public void RequestDropItem(Item item, int count)
	{
		if (item == null || count <= 0)
			return;

		_world.Sync.RpcId(1, nameof(WorldSync.RequestDropItem), item.Id, count);
	}

	public void GiveItem(Item item, int count)
	{
		InventoryManager.Instance.AddItem(this, item, count);
	}

	// tool usage and hit reactions
	private void UseActiveTool()
	{
		if (!CanSwing)
			return;

		var tool = GetActiveTool();

		var swingDir = GetSwingDirection();
		if (swingDir.LengthSquared() < 0.001f)
			return;

		StartSwingCooldown(tool);
		PlayUseActiveToolVisual(tool, swingDir);
		RequestUseActiveTool(swingDir);
	}

	public void RequestUseActiveTool(Vector3 aimDir)
	{
		_world.Sync.RpcId(1, nameof(WorldSync.RequestUseActiveTool), aimDir);
	}

	public ToolItem GetActiveTool()
	{
		if (_equippedItem is ToolItem tool)
			return tool;

		return DefaultTool;
	}

	private void UpdateToolCooldown(float dt)
	{
		if (_hitCooldownAccum > 0f)
			_hitCooldownAccum -= dt;
	}

	public void StartSwingCooldown(ToolItem tool)
	{
		_hitCooldownAccum = tool.CooldownSeconds;
	}

	private void HandleToolUse(bool hudOpen)
	{
		if (hudOpen || !Input.IsActionPressed(UseToolAction))
			return;

		UseActiveTool();
	}

	private void HandlePlaceItem(bool hudOpen)
	{
		if (hudOpen || !Input.IsActionPressed(InteractAction))
			return;

		if (_equippedItem is PlaceableItem)
			if (_placement.TryPlace())
				UpdateEquippedItem();
	}

	private Vector3 GetSwingDirection()
	{
		var swingDir = _aimLockTimer > AimLockTime
			? Velocity.Normalized()
			: _aimDirection.Rotated(Vector3.Up, Mathf.DegToRad(45)).Normalized();

		if (swingDir.LengthSquared() < 0.001f)
			return Vector3.Zero;

		return swingDir.Normalized();
	}

	public void PlayUseActiveToolVisual(ToolItem tool, Vector3 swingDir)
	{
		switch (tool.ToolType)
		{
			case "axe":
			case "sword":
				_animTree.Set(AxeRequestPath, (int)AnimationNodeOneShot.OneShotRequest.Fire);
				AudioManager.Instance.PlayVariantAt("swing_blade_small", GlobalPosition, AudioManager.BusTools, 0.1f);

				break;
			default:
				_animTree.Set(PunchRequestPath, (int)AnimationNodeOneShot.OneShotRequest.Fire);
				AudioManager.Instance.PlayVariantAt("swing_fist", GlobalPosition, AudioManager.BusTools, 0.1f);
				break;
		}

		var targetAngle = Mathf.Atan2(swingDir.X, swingDir.Z);
		Rotation = new Vector3(Rotation.X, targetAngle, Rotation.Z);
	}

	public void PlayRemoteUseActiveToolVisual(ToolItem tool, Vector3 swingDir)
	{
		PlayUseActiveToolVisual(tool, swingDir);
	}

	public void HandleLocalAttackResult(ToolHitResult result)
	{
		switch (result.Outcome)
		{
			case ToolHitOutcome.Destroyed:
				CameraController?.Shake(0.3f, 0.7f);
				break;

			case ToolHitOutcome.Failed:
				CameraController?.Shake(0.1f, 0.3f);
				break;
		}
	}

	public void HandleAttackResult(ToolHitResult result)
	{
		switch (result.Outcome)
		{
			case ToolHitOutcome.Hit:
				PlayToolSound(result.HitSoundKey, result.HitPoint);
				break;

			case ToolHitOutcome.Destroyed:
				PlayToolSound(result.HitSoundKey, result.HitPoint);
				PlayToolSound(result.BreakSoundKey, result.HitPoint);
				break;

			case ToolHitOutcome.Failed:
				// play fail sound
				break;
		}
	}

	private void PlayToolSound(string key, Vector3 hitPoint)
	{
		if (string.IsNullOrEmpty(key))
			return;

		AudioManager.Instance.PlayVariantAt(
			key,
			hitPoint,
			AudioManager.BusTools,
			0.2f
		);
	}

	// biome
	public void CheckBiome()
	{
		var biome = _world.GetBiomeAtPos(GlobalPosition);
		if (biome != CurrentBiome)
			OnBiomeChanged(biome);
	}

	public void OnBiomeChanged(BiomeId newBiome)
	{
		CurrentBiome = newBiome;
		_tintOverlay.SetTintForBiome(newBiome);
		BiomeChanged?.Invoke(newBiome);
	}

	// networking
	private void SyncTransform()
	{
		RpcId(1, nameof(SubmitTransform), GlobalPosition, Velocity, Rotation.Y);
	}

	[Rpc(
		MultiplayerApi.RpcMode.AnyPeer,
		CallLocal = false,
		TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable
	)]
	public void SubmitTransform(Vector3 pos, Vector3 vel, float rotY)
	{
		var senderId = Multiplayer.GetRemoteSenderId();
		if (PlayerId != senderId)
			return;

		GlobalPosition = pos;
		Velocity = vel;
		Rotation = new Vector3(Rotation.X, rotY, Rotation.Z);

		Rpc(nameof(ReceiveTransform), senderId, pos, vel, rotY);
	}

	[Rpc(
		MultiplayerApi.RpcMode.Authority,
		CallLocal = false,
		TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable
	)]
	private void ReceiveTransform(int playerId, Vector3 pos, Vector3 vel, float rotY)
	{
		if (PlayerId != playerId)
			return;

		if (IsLocal)
			return;

		GlobalPosition = pos;
		Velocity = vel;
		Rotation = new Vector3(Rotation.X, rotY, Rotation.Z);
	}

	public void ApplyRemoteHitState(float health, Vector3 position, Vector3 knockbackVelocity)
	{
		Health = health;
		GlobalPosition = position;
		KnockbackVelocity = knockbackVelocity;

		HUD.RefreshUI();
	}


	#region IToolHittable

	public string GetImpactType()
	{
		return "flesh";
	}

	public string GetHitSound(ToolItem tool)
	{
		return tool.ToolType switch
		{
			"sword" => "hit_flesh_blade",
			"axe" => "hit_flesh_blade",
			_ => "hit_flesh"
		};
	}

	public string GetBreakSound()
	{
		return "hit_flesh";
	}

	public Node3D GetHitRoot()
	{
		return this;
	}

	public ToolHitOutcome ReceiveToolHit(ToolItem tool, float damage, Vector3 fromDirection, Vector3 hitPoint)
	{
		ApplyKnockback(fromDirection, 3f);
		Health -= damage;

		_world.Sync.SendPlayerHitState(this);

		return Health <= 0f ? ToolHitOutcome.Destroyed : ToolHitOutcome.Hit;
	}

	public ToolHitOutcome ReceiveToolHitFailed(ToolItem tool, Vector3 fromDirection, Vector3 hitPoint)
	{
		return ToolHitOutcome.Failed;
	}

	#endregion

	private void ApplyKnockback(Vector3 direction, float strength)
	{
		if (direction.LengthSquared() < 0.001f)
			return;

		KnockbackVelocity += direction.Normalized() * (strength / _knockbackResistance);
	}

	private static IInteractable FindInteractable(Node node)
	{
		while (node != null)
		{
			if (node is IInteractable interactable)
				return interactable;

			node = node.GetParent();
		}

		return null;
	}
}