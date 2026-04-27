using Godot;
using System.Collections.Generic;

public partial class HUD : CanvasLayer
{
	private PackedScene _slotPanelScene =
		ResourceLoader.Load<PackedScene>("res://scenes/ui/HUD/ItemContainerSlot.tscn");

	private Inventory _inventory;
	private Hotbar _hotbar;
	private IItemContainer _storage;

	private Control _cursorItem;
	private TextureRect _cursorIcon;
	private Label _cursorCount;

	private Control _inventoryRoot;
	private PanelContainer _inventoryWindow;
	private GridContainer _inventorySlotGrid;
	private PanelContainer _storageWindow;
	private GridContainer _storageSlotGrid;
	private Label _storageLabel;

	private HBoxContainer _hotbarBox;

	private CraftingUI _craftingUI;

	private Tooltip _tooltipManager;
	private PanelContainer _tooltip;

	private readonly List<ItemContainerSlot> _hotbarSlots = new();
	private readonly List<ItemContainerSlot> _inventorySlots = new();
	private readonly List<ItemContainerSlot> _storageSlots = new();

	private Player _player;
	private Label _healthLabel;

	public bool WindowOpen => _inventoryRoot.Visible || _craftingUI.Visible || _storageWindow.Visible;
	public bool IsCraftingOpen => _craftingUI.Visible;
	public bool IsInventoryOpen => _inventoryRoot.Visible;
	private bool _uiReady;

	public override void _Ready()
	{
		_cursorItem = GetNode<Control>("CursorItem");
		_cursorIcon = _cursorItem.GetNode<TextureRect>("Container/Icon");
		_cursorCount = _cursorItem.GetNode<Label>("Container/Label");
		_inventoryRoot = GetNode<Control>("Inventory");
		_inventoryWindow = _inventoryRoot.GetNode<PanelContainer>("HBoxContainer/InventoryWindow");
		_inventorySlotGrid = _inventoryWindow.GetNode<GridContainer>("MarginContainer/SlotGrid");
		_storageWindow = _inventoryRoot.GetNode<PanelContainer>("HBoxContainer/StorageWindow");
		_storageSlotGrid = _storageWindow.GetNode<GridContainer>("MarginContainer/SlotGrid");
		_storageLabel = _storageWindow.GetNode<Label>("Label");
		_healthLabel = GetNode<Label>("HealthContainer/HealthLabel");

		_hotbarBox = GetNode<HBoxContainer>("MarginContainer/Hotbar");

		_craftingUI = GetNode<CraftingUI>("Crafting");

		_tooltipManager = GetNode<Tooltip>("TooltipManager");
		_tooltip = _tooltipManager.GetNode<PanelContainer>("Tooltip");

		GameManager.Instance.LocalPlayerChanged += OnLocalPlayerChanged;

		GetWindow().ContentScaleFactor = 1.25f;
	}

	private void OnLocalPlayerChanged()
	{
		if (_player != null)
			_player.PlayerReady -= OnPlayerReady;

		_player = GameManager.Instance.LocalPlayer;
		_uiReady = false;

		if (_player == null) return;

		OnPlayerReady();
	}

	private void OnPlayerReady()
	{
		_inventory = _player.GetNode<Inventory>("Inventory");
		_hotbar = _player.GetNode<Hotbar>("Hotbar");

		_inventory.ContainerChanged += RefreshUI;
		_hotbar.ContainerChanged += RefreshUI;
		_hotbar.SelectedSlotChanged += OnHotbarSelectionChanged;

		_inventoryRoot.Visible = false;
		_craftingUI.Visible = false;
		_storageWindow.Visible = false;

		BuildInventorySlots();
		BuildHotbarSlots();
		UpdateHotbarHighlight();
		RefreshUI();

		_uiReady = true;
	}

	public override void _Process(double delta)
	{
		if (!_uiReady) return;

		if (_player.DraggedStack != null)
		{
			var mousePos = GetViewport().GetMousePosition();
			_cursorItem.GlobalPosition = mousePos + new Vector2(6, 6);
		}

		_healthLabel.Text = $"Health: {_player.Health}/{_player.MaxHealth}";
	}

	public void OpenInventoryUI()
	{
		CloseCraftingUI();

		AudioManager.Instance.PlayVariant("ui_inventory_open");
		_inventoryRoot.Visible = true;
	}

	public void CloseInventoryUI()
	{
		if (_player.DraggedStack != null) DropStack();

		AudioManager.Instance.PlayVariant("ui_inventory_close");

		_inventoryRoot.Visible = false;
		_craftingUI.Visible = false;
		_tooltip.Visible = false;

		CloseStorageUI();
	}

	public void OpenStorageUI(IItemContainer storage)
	{
		_storage = storage;
		OpenInventoryUI();

		BuildStorageSlots(storage);
		_storageWindow.Visible = true;

		if (storage is Node node && node.HasSignal("ContainerChanged"))
			node.Connect("ContainerChanged", new Callable(this, nameof(RefreshUI)));
	}

	private void CloseStorageUI()
	{
		if (_storage is Node node && node.HasSignal("ContainerChanged"))
			node.Disconnect("ContainerChanged", new Callable(this, nameof(RefreshUI)));

		_storage = null;
		_storageWindow.Visible = false;
		ClearChildren(_storageSlotGrid);
		_storageSlots.Clear();
	}

	public void OpenCraftingUI(ICraftingStation station = null)
	{
		CloseInventoryUI();
		_craftingUI.OpenForStation(station);
		_craftingUI.Visible = true;
	}

	public void CloseCraftingUI()
	{
		AudioManager.Instance.PlayVariant("ui_inventory_close");
		_craftingUI.Visible = false;
		_inventoryRoot.Visible = false;
		_tooltip.Visible = false;
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (e is InputEventMouseButton { Pressed: true } mb)
		{
			if (mb.ButtonIndex == MouseButton.WheelUp)
				_hotbar.SelectPrev();
			else if (mb.ButtonIndex == MouseButton.WheelDown)
				_hotbar.SelectNext();

			if (_player.DraggedStack != null && IsCursorOutsideInventory()) DropStack();
		}

		if (e.IsActionPressed("ui_cancel"))
		{
			if (WindowOpen)
			{
				CloseCraftingUI();
				CloseInventoryUI();
				CloseStorageUI();
			}
			else
			{
				if (MenuManager.Instance.HasMenus)
				{
					var top = MenuManager.Instance.Peek();

					if (top is PauseMenu pm)
					{
						pm.Close();
						return;
					}

					MenuManager.Instance.Pop();
					return;
				}

				MenuManager.Instance.GetNode<PauseMenu>("PauseMenu").Open();
			}
		}

		for (var i = 0; i < 9; i++)
			if (e.IsActionPressed($"hotbar_{i + 1}"))
				_hotbar.SelectSlot(i);
	}

	private void ClearChildren(Node parent)
	{
		foreach (var child in parent.GetChildren())
			if (child is GodotObject go && IsInstanceValid(go))
				go.Free();
	}

	private void BuildInventorySlots()
	{
		ClearChildren(_inventorySlotGrid);
		_inventorySlots.Clear();

		for (var i = 0; i < _inventory.SlotCount; i++)
		{
			var slot = _slotPanelScene.Instantiate<ItemContainerSlot>();
			slot.SetSlot(_inventory, i);
			slot.SetStack(_inventory[i]);
			slot.SlotLeftClicked += OnSlotLeftClick;
			slot.SlotRightClicked += OnSlotRightClick;
			slot.SlotShiftClicked += OnSlotShiftLeftClick;

			_inventorySlotGrid.AddChild(slot);
			_inventorySlots.Add(slot);
		}
	}

	private void BuildHotbarSlots()
	{
		ClearChildren(_hotbarBox);
		_hotbarSlots.Clear();

		for (var i = 0; i < _hotbar.SlotCount; i++)
		{
			var slot = _slotPanelScene.Instantiate<ItemContainerSlot>();
			slot.SetSlot(_hotbar, i);
			slot.SetStack(_hotbar[i]);
			slot.SlotLeftClicked += OnSlotLeftClick;
			slot.SlotRightClicked += OnSlotRightClick;
			slot.SlotShiftClicked += OnSlotShiftLeftClick;

			_hotbarBox.AddChild(slot);
			_hotbarSlots.Add(slot);
		}
	}

	private void BuildStorageSlots(IItemContainer storage)
	{
		ClearChildren(_storageSlotGrid);
		_storageSlots.Clear();

		for (var i = 0; i < storage.SlotCount; i++)
		{
			var slot = _slotPanelScene.Instantiate<ItemContainerSlot>();
			slot.SetSlot(storage, i);
			slot.SetStack(storage.GetSlot(i));
			slot.SlotLeftClicked += OnSlotLeftClick;
			slot.SlotRightClicked += OnSlotRightClick;
			slot.SlotShiftClicked += OnSlotShiftLeftClick;

			_storageSlotGrid.AddChild(slot);
			_storageSlots.Add(slot);
		}

		_storageSlotGrid.Columns = Mathf.CeilToInt(storage.SlotCount / 3.0f);
		_storageLabel.Text = storage.Label;

		RefreshUI();
	}

	public void RefreshUI()
	{
		if (_inventory == null || _hotbar == null) return;

		for (var i = 0; i < _inventory.SlotCount; i++) _inventorySlots[i].SetStack(_inventory[i]);

		for (var i = 0; i < _hotbar.SlotCount; i++) _hotbarSlots[i].SetStack(_hotbar[i]);

		if (_storage != null)
			for (var i = 0; i < _storage.SlotCount; i++)
				_storageSlots[i].SetStack(_storage.GetSlot(i));
	}

	private void OnHotbarSelectionChanged(int selectedIndex)
	{
		UpdateHotbarHighlight();
	}

	private void UpdateHotbarHighlight()
	{
		for (var i = 0; i < _hotbarSlots.Count; i++)
		{
			var slot = _hotbarSlots[i];
			slot.ScaleTween?.Kill();
			slot.ScaleTween = null;
			slot.Scale = Vector2.One;
			slot.Highlight.Visible = false;

			if (i == _hotbar.SelectedSlot)
			{
				slot.Highlight.Visible = true;

				slot.ScaleTween = CreateTween();
				slot.ScaleTween.TweenProperty(slot, "scale", new Vector2(1.2f, 1.2f), 0.1f)
					.SetTrans(Tween.TransitionType.Back)
					.SetEase(Tween.EaseType.Out);
			}
		}
	}

	private void OnSlotLeftClick(IItemContainer container, int index)
	{
		var world = GameManager.Instance.CurrentWorld;
		if (world == null)
			return;

		var slotBefore = container.GetSlot(index);
		var draggedBefore = _player.DraggedStack;

		var kind = GetContainerKind(container);
		var storageTileCoord = GetStorageTileCoord(container);

		world.Sync.HandleContainerClick(
			kind,
			storageTileCoord,
			index,
			0,
			false);

		if (draggedBefore == null && slotBefore is { Count: > 0 })
			AudioManager.Instance.PlaySfx(UiSfxResolver.GetInventorySfx(slotBefore, InventorySfxAction.Pickup), 0.1f);
		else if (draggedBefore is { Count: > 0 })
			AudioManager.Instance.PlaySfx(UiSfxResolver.GetInventorySfx(draggedBefore, InventorySfxAction.Drop), 0.1f);

		if (!world.Multiplayer.HasMultiplayerPeer() || world.Multiplayer.IsServer())
		{
			UpdateCursor();
			RefreshUI();
		}
	}

	private void OnSlotShiftLeftClick(IItemContainer source, int index)
	{
		var world = GameManager.Instance.CurrentWorld;
		if (world == null)
			return;

		var kind = GetContainerKind(source);
		var storageTileCoord = GetStorageTileCoord(source);

		world.Sync.HandleContainerClick(
			kind,
			storageTileCoord,
			index,
			0,
			true
		);

		if (!world.Multiplayer.HasMultiplayerPeer() || world.Multiplayer.IsServer())
			RefreshUI();
	}

	private void OnSlotRightClick(IItemContainer container, int index)
	{
		var world = GameManager.Instance.CurrentWorld;
		if (world == null)
			return;

		var kind = GetContainerKind(container);
		var storageTileCoord = GetStorageTileCoord(container);

		world.Sync.HandleContainerClick(
			kind,
			storageTileCoord,
			index,
			1,
			false
		);

		if (!world.Multiplayer.HasMultiplayerPeer() || world.Multiplayer.IsServer())
		{
			UpdateCursor();
			RefreshUI();
		}
	}

	private void DropStack()
	{
		if (_player.DraggedStack == null)
			return;

		_player.RequestDropItem(_player.DraggedStack.Item, _player.DraggedStack.Count);

		UpdateCursor();
		RefreshUI();
	}

	private void UpdateCursor()
	{
		if (_player.DraggedStack == null)
		{
			_cursorItem.Visible = false;
			return;
		}

		_cursorItem.Visible = true;
		_cursorIcon.Texture = _player.DraggedStack.Item.Icon;
		_cursorCount.Text = _player.DraggedStack.Count > 1 ? _player.DraggedStack.Count.ToString() : "";
	}

	private bool IsCursorOutsideInventory()
	{
		if (!_inventoryRoot.Visible)
			return false;

		var mousePos = GetViewport().GetMousePosition();
		return !_inventoryWindow.GetGlobalRect().HasPoint(mousePos);
	}

	public void UpdateDraggedCursorFromPlayerState()
	{
		UpdateCursor();
	}

	private ContainerKind GetContainerKind(IItemContainer container)
	{
		if (ReferenceEquals(container, _inventory))
			return ContainerKind.Inventory;

		if (ReferenceEquals(container, _hotbar))
			return ContainerKind.Hotbar;

		if (ReferenceEquals(container, _storage))
			return ContainerKind.Storage;

		GD.PrintErr("Unknown container kind in HUD.");
		return ContainerKind.Inventory;
	}

	private Vector2I GetStorageTileCoord(IItemContainer container)
	{
		if (!ReferenceEquals(container, _storage))
			return Vector2I.Zero;

		if (container is not WorldObject worldObject)
			return Vector2I.Zero;

		return worldObject.Data.TileCoord;
	}

	public override void _ExitTree()
	{
		if (_inventory != null) _inventory.ContainerChanged -= RefreshUI;

		if (_hotbar != null)
		{
			_hotbar.ContainerChanged -= RefreshUI;
			_hotbar.SelectedSlotChanged -= OnHotbarSelectionChanged;
		}

		if (_player != null) _player.PlayerReady -= OnPlayerReady;

		if (GameManager.Instance != null)
			GameManager.Instance.LocalPlayerChanged -= OnLocalPlayerChanged;
	}
}