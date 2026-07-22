using System;
using Godot;

public partial class Player : CharacterBody3D, IToolHittable
{
	[Signal]
	public delegate void PlayerReadyEventHandler();

	public event Action<BiomeId> BiomeChanged;

	private static readonly PackedScene CameraControllerScene =
		GD.Load<PackedScene>("res://scenes/entities/player/CameraController.tscn");

	private static readonly StringName InteractAction = "interact";
	private static readonly StringName PlaceItemAction = "place_item";

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
	public CombatController CombatController;
	public CombatIntent CombatIntent => _combatDriver?.CombatIntent ?? CombatIntent.None;
	public HUD HUD;
	public Hotbar Hotbar;
	public Inventory Inventory;
	public ItemStack DraggedStack;
	public CameraController CameraController;
	public PhysicsRayQueryParameters3D ToolQuery;

	private World _world;
	private EntityMotor _entityMotor;
	private EntityAnimationController _animationController;
	private PlayerInteractionController _interactionController;
	private PlayerCombatDriver _combatDriver;
	private PlayerEquipment _equipment;
	private PlacementController _placement;
	private BiomeTintOverlay _tintOverlay;
	private AnimationTree _animTree;
	private Label3D _nameplate;

	private float _aimLockTimer;
	private float _transformSyncTimer;
	private float _remoteTargetRotY;

	private Vector3 _lastCheckedPosition;
	private Vector3 _aimDirection = Vector3.Forward;
	private Vector3 _remoteTargetPosition;
	private Vector3 _remoteTargetVelocity;

	private bool _testItemsGiven;
	private bool _suppressSelectedSlotRequest;
	private bool _hasRemoteTransform;

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
		_equipment.Init(DefaultTool);

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
		InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("stone_axe"), 1);
		InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("stone_pickaxe"), 1);
		InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("iron_shield"), 1);
		InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("chest"), 1);
		InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("crafting_table"), 1);
		// InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("kiln"), 1);
		// InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("copper_ore"), 99);
		// InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("coal"), 99);
		// InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("wood"), 99);
		// InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("campfire"), 2);

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

		_combatDriver = new PlayerCombatDriver();
		_combatDriver.Init(this, _world, _equipment, _entityMotor, _animationController, CombatController,
			UpdateAimDirection, GetSwingDirection);
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

		if (Input.IsActionJustPressed("equip_offhand"))
			EquipOffhand();

		_combatDriver.TickLocal(dt, hudOpen);
		HandleLocalMovement(dt);
		_animationController.Tick(blendAlpha);
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
		_combatDriver.OnAttackHitFrame();
	}

	public void OnAttackHoldFrame()
	{
		_combatDriver.OnAttackHoldFrame(IsLocal);
	}

	public void Anim_AttackSwingFrame()
	{
		_combatDriver.OnAttackSwingFrame();
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
		var moveVelocity = hasMovementInput
			? HandleMovementInput(inputDir, dt)
			: Vector3.Zero;

		if (!hasMovementInput)
			_aimLockTimer = 0f;

		if (CombatController.IsBlocking)
		{
			UpdateAimDirection();
			_animationController.FaceDirection(_aimDirection);
		}

		_entityMotor.Update(dt, moveVelocity);
		MoveAndSlide();

		_animationController.UpdateLocomotionBlend(_entityMotor.MovementVelocity, Speed);
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
		if (hudOpen || !Input.IsActionJustPressed(PlaceItemAction))
			return;

		if (GetSelectedHotbarItem() is PlaceableItem && _placement.TryPlace())
			UpdateEquippedItem();
	}

	// equipped item and inventory sync
	private Item GetSelectedHotbarItem()
	{
		var stack = Hotbar.GetSlot(Hotbar.SelectedSlot);
		return stack?.Item;
	}

	private void UpdateEquippedItem()
	{
		var newItem = GetSelectedHotbarItem();
		var changed = _equipment.UpdateHeldItem(newItem);

		if (IsLocal && (changed || _placement.Active))
			UpdatePlacementState(newItem);
	}

	private void EquipOffhand()
	{
		if (!_equipment.TryEquipOffhand(GetSelectedHotbarItem(), out var tool))
			return;

		if (IsLocal)
			_world.Sync.RequestOffhandItemChange(this, tool.Id);
	}

	private void OnSelectedSlotChanged(int slotIndex)
	{
		UpdateEquippedItem();

		if (IsLocal && !_suppressSelectedSlotRequest)
			_world.Sync.RequestSelectedHotbarSlotChange(slotIndex);
	}

	private void OnHotbarContainerChanged()
	{
		UpdateEquippedItem();
		_world.Sync.BroadcastHeldItem(this);
	}

	public string GetOffhandToolId()
	{
		return _equipment.OffhandToolId;
	}

	public void HandleSelectedSlotChanged(int slotIndex)
	{
		if (!SetSelectedSlotSilently(slotIndex))
			return;

		_world.Sync.BroadcastSelectedHotbarSlot(this, slotIndex);
		_world.Sync.BroadcastHeldItem(this);
	}

	public void HandleOffhandItemChanged(string itemId)
	{
		if (!_equipment.TrySetOffhandById(itemId))
			return;

		_world.Sync.BroadcastHeldItem(this);
	}

	public void ApplyRemoteSelectedSlot(int slotIndex)
	{
		SetSelectedSlotSilently(slotIndex);
	}

	public void ApplyRemoteHeldItem(int slotIndex, string itemId, string offhandItemId)
	{
		SetSelectedSlotSilently(slotIndex);

		var item = ItemRegistry.GetItem(itemId) ?? DefaultTool;
		_equipment.UpdateHeldItem(item);

		_equipment.TrySetOffhandById(offhandItemId);
	}

	private bool SetSelectedSlotSilently(int slotIndex)
	{
		if (slotIndex < 0 || slotIndex >= Hotbar.SlotCount)
			return false;

		_suppressSelectedSlotRequest = true;
		Hotbar.SelectSlot(slotIndex);
		_suppressSelectedSlotRequest = false;
		return true;
	}

	public void RequestItemPickup(ulong pickupId)
	{
		_world.Sync.RequestItemPickup(pickupId);
	}

	public void RequestDropItem(Item item, int count)
	{
		if (item == null || count <= 0)
			return;

		_world.Sync.RequestItemDrop(item.Id, count);
	}

	public void GiveItem(Item item, int count)
	{
		InventoryManager.Instance.AddItem(this, item, count);
	}

	// tool usage and hit reactions
	public ToolItem GetActiveTool()
	{
		return _equipment.ActiveTool;
	}

	public ToolItem GetBlockingTool()
	{
		return _equipment.BlockingTool;
	}

	public void RequestUseActiveTool(Vector3 aimDir, bool isCharged)
	{
		_combatDriver.RequestUseActiveTool(aimDir, isCharged);
	}

	private Vector3 GetSwingDirection()
	{
		return _aimDirection.LengthSquared() < 0.001f ? Vector3.Zero : _aimDirection.Normalized();
	}

	public void HandleLocalAttackResult(ToolHitResult result)
	{
		_combatDriver.HandleLocalAttackResult(result);
	}

	public void PlayRemoteCombatAnim(PlayerCombatAnimEvent animEvent, ToolItem tool, Vector3 swingDir, int comboIndex,
		int sequence)
	{
		_combatDriver.PlayRemoteCombatAnim(animEvent, tool, swingDir, comboIndex, sequence);
	}

	public void HandleAttackResult(ToolHitResult result)
	{
		_combatDriver.HandleAttackResult(result);
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

		_animationController.UpdateLocomotionBlend(_entityMotor.MovementVelocity, Speed);
		_animationController.Tick(blendAlpha);
		_combatDriver.TickRemoteCombatVisual(dt);

		_entityMotor.Update(dt, moveVelocity);
		MoveAndSlide();
	}

	public void SetCombatIntent(CombatIntent intent)
	{
		_combatDriver.SetCombatIntent(intent);
	}

	public void ApplyRemoteHitEvent(float health, Vector3 hitDirection, float knockback)
	{
		_combatDriver.ApplyRemoteHitEvent(health, hitDirection, knockback);
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
		var blocked = false;
		var blockingTool = GetBlockingTool();

		if (blockingTool != null && CombatController.IsBlocking && CombatUtils.IsBlockingHit(-GlobalTransform.Basis.Z,
			    fromDirection,
			    blockingTool.BlockStats.ArcDegrees))
		{
			var blockStats = blockingTool.BlockStats;
			damage *= 1f - blockStats.DamageReduction;
			knockback *= 1f - blockStats.KnockbackReduction;
			stagger *= 1f - blockStats.PoiseReduction;
			blocked = true;
		}

		Health -= damage;

		if (blocked)
			return ToolHitResponse.Blocked(knockback, blockingTool);

		return Health <= 0f ? ToolHitResponse.Destroyed(knockback) : ToolHitResponse.Hit(knockback);
	}

	public ToolHitResponse ReceiveToolHitFailed(ToolItem tool, Vector3 fromDirection, Vector3 hitPoint)
	{
		return ToolHitResponse.Failed();
	}

	#endregion
}