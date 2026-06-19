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
	private static readonly StringName InteractAction = "interact";

	private const float BiomeCheckDistance = 0.5f;
	private const uint HittableMask = 1u << 1;

	public int PlayerId { get; set; }
	public bool IsLocal { get; set; }

	public float MaxHealth = 20f;
	public float Health;
	public float Speed = 5.0f;
	public float AimLockTime = 0.5f;
	public ToolItem DefaultTool;
	public BiomeId CurrentBiome = BiomeId.Unknown;

	private EntityMotor _entityMotor;
	private CombatController _combatController;
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

	private bool _testItemsGiven;
	private bool _suppressSelectedSlotRequest;

	private Item _equippedItem;
	private Item _lastEquippedItem;
	private PlayerEquipment _equipment;
	private PlacementController _placement;

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
			InventoryManager.Instance.AddItem(this, ItemRegistry.GetItem("stone_pickaxe"), 1);
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

		_entityMotor = new EntityMotor();
		AddChild(_entityMotor);
		_entityMotor.Init(this, _world);

		_animationController = new EntityAnimationController();
		AddChild(_animationController);
		_animationController.Init(this, _animTree);

		if (!IsLocal)
			return;

		_interactionController = new PlayerInteractionController();
		AddChild(_interactionController);
		_interactionController.Init(this);

		_combatController = new CombatController();
		AddChild(_combatController);
		_combatController.Init(this);

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

		var isMoving = Velocity.LengthSquared() > 0.01f;

		var blendAlpha = 1f - Mathf.Exp(-14f * dt);

		if (!IsLocal)
		{
			UpdateRemotePlayer(dt, blendAlpha, isMoving);
			return;
		}

		var viewport = GetViewport();
		var camera = viewport.GetCamera3D();
		var hudOpen = HUD.WindowOpen;

		if (_combatController.Tick(dt) && !_combatController.IsCharged) _animationController.ReturnToIdle();

		HandleLocalMovement(dt);
		_animationController.Tick(blendAlpha);
		HandleToolUse(dt);
		HandlePlaceItem(hudOpen);
		HandleInteraction(hudOpen);
		UpdatePlacementOrFocus(dt, camera, viewport, hudOpen);
		_world.Sync.SyncTransform(this);
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

	public void OnAttackHoldFrame()
	{
		_animationController.HoldCurrentAttackPose();
		_combatController.OpenComboWindow();
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

	// movement and animation
	private void UpdateRemotePlayer(float dt, float blendAlpha, bool isMoving)
	{
		_animationController.SetLocomotionState(isMoving);
		_animationController.Tick(blendAlpha);

		_entityMotor.Update(dt, Velocity);
		MoveAndSlide();
	}

	private void HandleLocalMovement(float dt)
	{
		var inputDir = GetMovementInput();
		var moveVelocity = Vector3.Zero;

		if (inputDir != Vector2.Zero)
		{
			moveVelocity = HandleMovementInput(inputDir, dt);
			_animationController.SetLocomotionState(true);
		}
		else
		{
			_aimLockTimer = 0f;
			_animationController.SetLocomotionState(false);
		}

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

		if (_entityMotor.LungeVelocity.LengthSquared() <= 0f)
			Rotation = new Vector3(
				Rotation.X,
				Mathf.LerpAngle(Rotation.Y, targetAngle, 10f * dt),
				Rotation.Z
			);

		var speedMultiplier = 1f;

		if (_combatController.AttackInProgress || _combatController.IsComboWindowOpen)
			speedMultiplier = 0.5f;

		if (_combatController.IsCharged || _entityMotor.LungeVelocity.LengthSquared() > 0f)
			speedMultiplier = 0f;

		return moveVec * Speed * speedMultiplier;
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
		if (!ignoreCooldown && !_combatController.CanSwing)
			return;

		var tool = GetActiveTool();

		UpdateAimDirection();
		var swingDir = GetSwingDirection();
		if (swingDir.LengthSquared() < 0.001f)
			return;

		_combatController.StartAttack();
		_combatController.StartCooldown(tool);

		var comboIndex = isCharged ? 0 : _combatController.ConsumeComboIndex(tool);
		_animationController.PlayUseTool(tool, swingDir, isCharged, comboIndex);

		if (isCharged && tool.ChargedLungeDistance > 0f)
			_entityMotor.StartLunge(swingDir, tool.ChargedLungeDistance, tool.ChargedLungeDuration);

		RequestUseActiveTool(swingDir, isCharged);
		SyncCombatAnim(isCharged ? PlayerCombatAnimEvent.ChargeRelease : PlayerCombatAnimEvent.LightAttack, tool.Id,
			swingDir);
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
			_combatController.CancelCharge();
			return;
		}

		if (Input.IsActionJustPressed(UseToolAction)) _combatController.StartChargeBuffer();

		if (_combatController.TickCharge(dt))
		{
			_animationController.PlayChargeStart(GetActiveTool());
			SyncCombatAnim(PlayerCombatAnimEvent.ChargeStart, GetActiveTool().Id, GetSwingDirection());
		}

		if (Input.IsActionJustReleased(UseToolAction))
		{
			_combatController.ReleaseCharge(out var chargedAttack);

			if (chargedAttack)
			{
				UseActiveTool(true, true);
			}
			else if (_combatController.QueuedAttack == QueuedAttackType.None)
			{
				_combatController.CancelCharge();

				if (_combatController.CanSwing && !_combatController.AttackInProgress)
					UseActiveTool(false, true);
				else
					_combatController.QueueLightAttack();
			}
		}

		if (_combatController.TryConsumeQueuedAttack(out var queued))
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

	private void HandlePlaceItem(bool hudOpen)
	{
		if (hudOpen || !Input.IsActionJustPressed(InteractAction))
			return;

		if (_equippedItem is PlaceableItem)
			if (_placement.TryPlace())
				UpdateEquippedItem();
	}

	private Vector3 GetSwingDirection()
	{
		var swingDir = _aimDirection;

		if (swingDir.LengthSquared() < 0.001f)
			return Vector3.Zero;

		return swingDir.Normalized();
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

	public void PlayRemoteCombatAnim(PlayerCombatAnimEvent animEvent, ToolItem tool, Vector3 swingDir)
	{
		_animationController.PlayCombatAnim(animEvent, tool, swingDir);
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
	public void ApplyRemoteTransform(Vector3 pos, Vector3 vel, float rotY)
	{
		if (IsLocal)
			return;

		GlobalPosition = pos;
		Velocity = vel;
		Rotation = new Vector3(Rotation.X, rotY, Rotation.Z);
	}

	private void SyncCombatAnim(PlayerCombatAnimEvent animEvent, string toolId, Vector3 swingDir)
	{
		if (!IsLocal)
			return;

		_world.Sync.SyncCombatAnim(animEvent, toolId, swingDir);
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

	public ToolHitOutcome ReceiveToolHit(ToolItem tool, float damage, float knockback, Vector3 fromDirection,
		Vector3 hitPoint)
	{
		Health -= damage;

		_world.Sync.SendPlayerHitEvent(this, fromDirection, knockback);

		return Health <= 0f ? ToolHitOutcome.Destroyed : ToolHitOutcome.Hit;
	}

	public ToolHitOutcome ReceiveToolHitFailed(ToolItem tool, Vector3 fromDirection, Vector3 hitPoint)
	{
		return ToolHitOutcome.Failed;
	}

	#endregion
}