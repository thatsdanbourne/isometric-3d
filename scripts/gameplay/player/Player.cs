using System;
using Godot;

public partial class Player : CharacterBody3D, IToolHittable
{
	[Signal]
	public delegate void PlayerReadyEventHandler();

	public event Action<BiomeId> BiomeChanged;

	private static readonly PackedScene CameraControllerScene =
		GD.Load<PackedScene>("res://scenes/entities/player/CameraController.tscn");

	private static readonly StringName UseToolAction = "use_tool";
	private static readonly StringName BlockAction = "block";
	private static readonly StringName InteractAction = "interact";

	private const float BiomeCheckDistance = 0.5f;
	private const float BiomeCheckDistanceSquared = BiomeCheckDistance * BiomeCheckDistance;
	private const uint HittableMask = 1u << 1;
	private const float TransformSyncInterval = 1f / 20f;
	private const float RemotePositionCorrection = 12f;
	private const float RemoteRotationCorrection = 14f;
	private const float RemoteSnapDistance = 4f;
	private const float RemoteSnapDistanceSquared = RemoteSnapDistance * RemoteSnapDistance;
	private const float RemoteMovementThresholdSquared = 0.01f;
	private const float MovementRotationSpeed = 5f;
	private const float CombatMovementMultiplier = 0.5f;

	public int PlayerId { get; set; }
	public bool IsLocal { get; set; }

	public float MaxHealth = 20f;
	public float Health;
	public float Speed = 5.0f;
	public float AimLockTime = 0.5f;
	public ToolItem DefaultTool;
	public BiomeId CurrentBiome = BiomeId.Unknown;

	private EntityMotor _entityMotor;
	public CombatController CombatController;
	private EntityAnimationController _animationController;
	private PlayerInteractionController _interactionController;

	public HUD HUD;
	public Hotbar Hotbar;
	public Inventory Inventory;
	public ItemStack DraggedStack;
	public CameraController CameraController;
	private Label3D _nameplate;

	private float _aimLockTimer;

	private Vector3 _lastCheckedPosition;
	private Vector3 _aimDirection = Vector3.Forward;
	private Vector3 _remoteTargetPosition;
	private Vector3 _remoteTargetVelocity;
	private float _remoteTargetRotY;
	private bool _hasRemoteTransform;

	private bool _testItemsGiven;
	private bool _suppressSelectedSlotRequest;
	private float _transformSyncTimer;
	private int _localCombatAnimSequence;
	private int _lastRemoteCombatAnimSequence;
	private float _remoteCombatReturnTimer;

	private Item _equippedItem;
	private Item _lastEquippedItem;
	private PlayerEquipment _equipment;
	private PlacementController _placement;

	private ToolItem _pendingAttackTool;
	private Vector3 _pendingAttackDir;
	private bool _pendingAttackCharged;

	private World _world;
	private BiomeTintOverlay _tintOverlay;
	private AnimationTree _animTree;
	public PhysicsRayQueryParameters3D ToolQuery;

	// lifecycle
	public override void _Ready()
	{
		Health = MaxHealth;
		DefaultTool = ItemRegistry.GetItem("fist") as ToolItem;
		_world = GameManager.Instance.CurrentWorld;

		CacheSceneNodes();
		ConfigureToolQuery();
		GiveTestingItemsIfNeeded();
		InitSharedControllers();

		if (!IsLocal)
			return;

		InitLocalControllers();
		EmitSignal(SignalName.PlayerReady);
	}

	private void CacheSceneNodes()
	{
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
	}

	private void ConfigureToolQuery()
	{
		ToolQuery = new PhysicsRayQueryParameters3D
		{
			CollideWithAreas = false,
			CollideWithBodies = true,
			CollisionMask = HittableMask
		};
	}

	private void GiveTestingItemsIfNeeded()
	{
		if (Multiplayer.HasMultiplayerPeer() && (!Multiplayer.IsServer() || _testItemsGiven))
			return;

		_testItemsGiven = true;

		// testing items
		InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("stone_sword"), 1);
		InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("stone_pickaxe"), 1);
		// InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("chest"), 1);
		// InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("crafting_table"), 1);
		// InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("kiln"), 1);
		// InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("copper_ore"), 99);
		// InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("coal"), 99);
		// InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("wood"), 99);
		InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("campfire"), 2);

		if (Multiplayer.HasMultiplayerPeer())
			_world.Sync.SyncPlayerInventoryState(this);
	}

	private void InitSharedControllers()
	{
		_entityMotor = new EntityMotor();
		AddChild(_entityMotor);
		_entityMotor.Init(this, _world);

		_animationController = new EntityAnimationController();
		AddChild(_animationController);
		_animationController.Init(this, _animTree);

		CombatController = new CombatController();
		AddChild(CombatController);
	}

	private void InitLocalControllers()
	{
		_interactionController = new PlayerInteractionController();
		AddChild(_interactionController);
		_interactionController.Init(this);

		CameraController = CameraControllerScene.Instantiate<CameraController>();
		CameraController.Player = this;
		AddChild(CameraController);

		HUD = GetNode<HUD>("/root/Bootstrap/ClientUI/HUD");
		HUD.RefreshUI();

		_placement = new PlacementController();
		AddChild(_placement);
		_placement.Init(_world, this);

		GetNode<AudioListener3D>("AudioListener3D").MakeCurrent();
	}

	public override void _Process(double delta)
	{
		if (_lastCheckedPosition.DistanceSquaredTo(GlobalPosition) < BiomeCheckDistanceSquared)
			return;

		_lastCheckedPosition = GlobalPosition;
		CheckBiome();
	}

	public override void _PhysicsProcess(double delta)
	{
		var dt = (float)delta;

		var blendAlpha = 1f - Mathf.Exp(-14f * dt);

		if (!IsLocal)
		{
			UpdateRemotePlayer(dt, blendAlpha);
			return;
		}

		var viewport = GetViewport();
		var camera = viewport.GetCamera3D();
		var hudOpen = HUD.WindowOpen;

		if (CombatController.Tick(dt) && !CombatController.IsCharged)
			_animationController.ReturnToIdle();

		HandleLocalMovement(dt);
		_animationController.Tick(blendAlpha);
		HandleToolUse(dt);
		HandlePlaceItem(hudOpen);
		HandleInteraction(hudOpen);
		UpdatePlacementOrFocus(dt, camera, viewport, hudOpen);
		SyncTransformTick(dt);
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

	// animation callbacks
	public void Anim_AttackHitFrame()
	{
		if (!CombatController.AttackInProgress)
			return;

		if (_pendingAttackTool == null)
			return;

		RequestUseActiveTool(_pendingAttackDir, _pendingAttackCharged);
	}

	public void OnAttackHoldFrame()
	{
		if (!IsLocal)
		{
			_animationController.HoldCurrentAttackPose();
			_remoteCombatReturnTimer = CombatController.ComboResetTime;
			return;
		}

		_animationController.HoldCurrentAttackPose();
		CombatController.OpenComboWindow();
	}

	// input and local gameplay
	private void ToggleInventoryUI()
	{
		if (HUD.IsInventoryOpen)
		{
			HUD.CloseInventoryUI();
			return;
		}

		if (_interactionController.FocusedInteractable is IItemContainer storage)
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

		_interactionController.TryInteract(true);
	}

	// local movement
	private void HandleLocalMovement(float dt)
	{
		var inputDir = GetMovementInput();
		var hasMovementInput = inputDir != Vector2.Zero;
		var moveVelocity = hasMovementInput ? HandleMovementInput(inputDir, dt) : Vector3.Zero;

		if (!hasMovementInput)
			_aimLockTimer = 0f;

		if (CombatController.IsBlocking)
		{
			UpdateAimDirection();
			_animationController.FaceDirection(_aimDirection);
		}

		_animationController.SetLocomotionState(hasMovementInput);
		_animationController.SetLocomotionBlend(Velocity.Length() / Speed);
		_entityMotor.Update(dt, moveVelocity);
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
		var isLunging = _entityMotor.LungeVelocity.LengthSquared() > 0f;
		var isAttacking = CombatController.AttackInProgress || CombatController.IsComboWindowOpen;
		var isMovementLocked = CombatController.IsCharged || isLunging;

		if (!isLunging && !isAttacking)
			SetRotationY(Mathf.LerpAngle(Rotation.Y, targetAngle, MovementRotationSpeed * dt));

		if (isMovementLocked)
			return Vector3.Zero;

		return moveVec * Speed * (isAttacking || CombatController.IsBlocking ? CombatMovementMultiplier : 1f);
	}

	private void SetRotationY(float rotationY)
	{
		Rotation = new Vector3(Rotation.X, rotationY, Rotation.Z);
	}

	// aiming, focus, placement
	private void UpdateAimDirection()
	{
		var viewport = GetViewport();
		var camera = viewport.GetCamera3D();
		if (camera != null)
			_aimDirection =
				CombatUtils.GetMouseAimDirection(camera, viewport.GetMousePosition(), GlobalPosition, _aimDirection);
	}

	private void UpdatePlacementOrFocus(float dt, Camera3D camera, Viewport viewport, bool hudOpen)
	{
		if (_placement.Active)
		{
			if (camera != null)
				_placement.Tick(camera, !hudOpen);
			return;
		}

		_interactionController.Tick(dt, camera, viewport.GetMousePosition(), !hudOpen);
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

	private void HandlePlaceItem(bool hudOpen)
	{
		if (hudOpen || !Input.IsActionJustPressed(InteractAction))
			return;

		if (_equippedItem is PlaceableItem && _placement.TryPlace())
			UpdateEquippedItem();
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
	private void UseActiveTool(bool isCharged, bool ignoreCooldown = false)
	{
		if (!ignoreCooldown && !CombatController.CanSwing)
			return;

		var tool = GetActiveTool();

		UpdateAimDirection();
		var swingDir = GetSwingDirection();
		if (swingDir.LengthSquared() < 0.001f)
			return;

		CombatController.StartAttack();
		CombatController.StartCooldown(tool);

		var comboIndex = isCharged ? 0 : CombatController.ConsumeComboIndex(tool);
		_animationController.PlayUseTool(tool, swingDir, isCharged, comboIndex);

		if (isCharged && tool.ChargedLungeDistance > 0f)
			_entityMotor.StartLunge(swingDir, tool.ChargedLungeDistance, tool.ChargedLungeDuration);

		_pendingAttackTool = tool;
		_pendingAttackDir = swingDir;
		_pendingAttackCharged = isCharged;

		// RequestUseActiveTool(swingDir, isCharged);
		SyncCombatAnim(isCharged ? PlayerCombatAnimEvent.ChargeRelease : PlayerCombatAnimEvent.LightAttack, tool.Id,
			swingDir, comboIndex);
	}

	public void RequestUseActiveTool(Vector3 aimDir, bool isCharged)
	{
		_world.Sync.RpcId(1, nameof(WorldSync.RequestUseActiveTool), aimDir, isCharged);
	}

	public ToolItem GetActiveTool()
	{
		if (_equippedItem is ToolItem tool)
			return tool;

		return DefaultTool;
	}

	private void HandleToolUse(float dt)
	{
		var hudOpen = HUD.WindowOpen;
		if (hudOpen)
		{
			CombatController.CancelCharge();
			return;
		}

		if (Input.IsActionPressed(BlockAction) && !CombatController.IsBlocking)
			if (CombatController.StartBlock())
			{
				_animationController.PlayBlockStart();
				_world.Sync.SetBlocking(true);
			}

		if (Input.IsActionJustReleased(BlockAction))
			if (CombatController.EndBlock())
			{
				_animationController.ReturnToIdle();
				_world.Sync.SetBlocking(false);
			}

		if (Input.IsActionJustPressed(UseToolAction)) CombatController.StartChargeBuffer();

		if (CombatController.TickCharge(dt))
		{
			var tool = GetActiveTool();
			_animationController.PlayChargeStart(tool);
			SyncCombatAnim(PlayerCombatAnimEvent.ChargeStart, tool.Id, GetSwingDirection(), 0);
		}

		if (Input.IsActionJustReleased(UseToolAction))
		{
			CombatController.ReleaseCharge(out var chargedAttack);

			if (chargedAttack)
			{
				UseActiveTool(true, true);
			}
			else if (CombatController.QueuedAttack == QueuedAttackType.None)
			{
				CombatController.CancelCharge();

				if (CombatController.CanSwing && !CombatController.AttackInProgress)
					UseActiveTool(false, true);
				else
					CombatController.QueueLightAttack();
			}
		}

		if (CombatController.TryConsumeQueuedAttack(out var queued))
			switch (queued)
			{
				case QueuedAttackType.Light:
					UseActiveTool(false, true);
					break;
				case QueuedAttackType.Charged:
					UseActiveTool(true, true);
					break;
			}
	}

	private Vector3 GetSwingDirection()
	{
		return _aimDirection.LengthSquared() < 0.001f ? Vector3.Zero : _aimDirection.Normalized();
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

	public void PlayRemoteCombatAnim(PlayerCombatAnimEvent animEvent, ToolItem tool, Vector3 swingDir, int comboIndex,
		int sequence)
	{
		if (sequence <= _lastRemoteCombatAnimSequence)
			return;

		_lastRemoteCombatAnimSequence = sequence;
		_remoteCombatReturnTimer = 0f;
		_animationController.PlayCombatAnim(animEvent, tool, swingDir, comboIndex);
	}

	public void HandleAttackResult(ToolHitResult result)
	{
		switch (result.Outcome)
		{
			case ToolHitOutcome.Hit:
				PlayToolSound(result.PrimarySoundKey, result.HitPoint);
				break;

			case ToolHitOutcome.Blocked:
				PlayToolSound(result.PrimarySoundKey, result.HitPoint);
				break;

			case ToolHitOutcome.Destroyed:
				PlayToolSound(result.PrimarySoundKey, result.HitPoint);
				PlayToolSound(result.BreakSoundKey, result.HitPoint);
				break;

			case ToolHitOutcome.Failed:
				PlayToolSound(result.PrimarySoundKey, result.HitPoint);
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
	private void SyncTransformTick(float dt)
	{
		_transformSyncTimer += dt;
		if (_transformSyncTimer < TransformSyncInterval)
			return;

		_transformSyncTimer = 0f;
		_world.Sync.SyncTransform(this);
	}

	public void ApplyRemoteTransform(Vector3 pos, Vector3 vel, float rotY)
	{
		if (IsLocal)
			return;

		_remoteTargetPosition = pos;
		_remoteTargetVelocity = vel;
		_remoteTargetRotY = rotY;
		_hasRemoteTransform = true;

		if (GlobalPosition.DistanceSquaredTo(pos) > RemoteSnapDistanceSquared)
			GlobalPosition = pos;

		Velocity = vel;
	}

	private void UpdateRemotePlayer(float dt, float blendAlpha)
	{
		var moveVelocity = Velocity;

		if (_hasRemoteTransform)
		{
			var positionError = _remoteTargetPosition - GlobalPosition;
			if (positionError.LengthSquared() > RemoteSnapDistanceSquared)
			{
				GlobalPosition = _remoteTargetPosition;
				positionError = Vector3.Zero;
			}

			moveVelocity = _remoteTargetVelocity + positionError * RemotePositionCorrection;
			SetRotationY(Mathf.LerpAngle(Rotation.Y, _remoteTargetRotY, RemoteRotationCorrection * dt));
		}

		var isMoving = _remoteTargetVelocity.LengthSquared() > RemoteMovementThresholdSquared ||
		               moveVelocity.LengthSquared() > RemoteMovementThresholdSquared;

		_animationController.SetLocomotionState(isMoving);
		_animationController.SetLocomotionBlend(Velocity.Length() / Speed);
		_animationController.Tick(blendAlpha);
		UpdateRemoteCombatVisual(dt);

		_entityMotor.Update(dt, moveVelocity);
		MoveAndSlide();
	}

	private void UpdateRemoteCombatVisual(float dt)
	{
		if (_remoteCombatReturnTimer <= 0f)
			return;

		_remoteCombatReturnTimer -= dt;
		if (_remoteCombatReturnTimer <= 0f)
			_animationController.ReturnToIdle();
	}

	private void SyncCombatAnim(PlayerCombatAnimEvent animEvent, string toolId, Vector3 swingDir, int comboIndex)
	{
		if (!IsLocal)
			return;

		_world.Sync.SyncCombatAnim(animEvent, toolId, swingDir, comboIndex, ++_localCombatAnimSequence);
	}

	public void ApplyRemoteHitEvent(float health, Vector3 hitDirection, float knockback)
	{
		Health = health;
		_entityMotor.ApplyKnockback(hitDirection, knockback);
		if (IsLocal)
			HUD.RefreshUI();
	}

	#region IToolHittable

	public string GetImpactType()
	{
		return "flesh";
	}

	public string GetBreakSound()
	{
		return "hit_flesh";
	}

	public Node3D GetHitRoot()
	{
		return this;
	}

	public void OnStaggered()
	{
	}

	public ToolHitResponse ReceiveToolHit(ToolItem tool, float damage, float knockback, float stagger,
		Vector3 fromDirection,
		Vector3 hitPoint)
	{
		var activeTool = GetActiveTool();
		var blocked = false;

		if (CombatController.IsBlocking && CombatUtils.IsBlockingHit(-GlobalTransform.Basis.Z, fromDirection,
			    activeTool.BlockStats.ArcDegrees))
		{
			damage *= 1f - activeTool.BlockStats.DamageReduction;
			knockback *= 1f - activeTool.BlockStats.KnockbackReduction;
			stagger *= 1f - activeTool.BlockStats.PoiseReduction;
			blocked = true;
		}

		Health -= damage;

		if (blocked)
			return ToolHitResponse.Blocked(knockback);

		return Health <= 0f ? ToolHitResponse.Destroyed(knockback) : ToolHitResponse.Hit(knockback);
	}

	public ToolHitResponse ReceiveToolHitFailed(ToolItem tool, Vector3 fromDirection, Vector3 hitPoint)
	{
		return ToolHitResponse.Failed();
	}

	#endregion
}