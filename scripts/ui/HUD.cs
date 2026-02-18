using Godot;
using System.Collections.Generic;

public partial class HUD : CanvasLayer
{
	private PackedScene _slotPanelScene =
		ResourceLoader.Load<PackedScene>("res://scenes/ui/HUD/ItemContainerSlot.tscn");

	private StyleBoxFlat _slotStyle =
		ResourceLoader.Load<StyleBoxFlat>("res://resources/ui/ItemContainerSlotStyle.tres");

	private StyleBoxFlat _slotHighlightStyle =
		ResourceLoader.Load<StyleBoxFlat>("res://resources/ui/ItemContainerSlotHighlight.tres");

	private Inventory _inventory;
	private Hotbar _hotbar;
	private IItemContainer _storage;

	private ItemStack _draggedStack;
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

	public bool WindowOpen => _inventoryRoot.Visible || _craftingUI.Visible || _storageWindow.Visible;
	public bool IsCraftingOpen => _craftingUI.Visible;
	public bool IsInventoryOpen => _inventoryRoot.Visible;

	public override void _Ready()
	{
		_cursorItem = GetNode<Control>("CursorItem");
		_cursorIcon = _cursorItem.GetNode<TextureRect>("Icon");
		_cursorCount = _cursorItem.GetNode<Label>("Label");

		GameManager.Instance.LocalPlayerChanged += (p) =>
		{
			_player = p;
			_player.PlayerReady += OnPlayerReady;
		};

		GetWindow().ContentScaleFactor = 1.25f;
	}

	private void OnPlayerReady()
	{
		_inventory = _player.GetNode<Inventory>("Inventory");
		_hotbar = _player.GetNode<Hotbar>("Hotbar");

		_inventoryRoot = GetNode<Control>("Inventory");
		_inventoryWindow = _inventoryRoot.GetNode<PanelContainer>("HBoxContainer/InventoryWindow");
		_inventorySlotGrid = _inventoryWindow.GetNode<GridContainer>("MarginContainer/SlotGrid");
		_storageWindow = _inventoryRoot.GetNode<PanelContainer>("HBoxContainer/StorageWindow");
		_storageSlotGrid = _storageWindow.GetNode<GridContainer>("MarginContainer/SlotGrid");
		_storageLabel = _storageWindow.GetNode<Label>("Label");

		_hotbarBox = GetNode<HBoxContainer>("MarginContainer/Hotbar");

		_craftingUI = GetNode<CraftingUI>("Crafting");

		_tooltipManager = GetNode<Tooltip>("TooltipManager");
		_tooltip = _tooltipManager.GetNode<PanelContainer>("Tooltip");

		BuildInventorySlots();
		BuildHotbarSlots();
		UpdateHotbarHighlight();

		_inventory.ContainerChanged += RefreshUI;
		_hotbar.ContainerChanged += RefreshUI;
		_hotbar.SelectedSlotChanged += OnHotbarSelectionChanged;

		_inventoryRoot.Visible = false;
		_craftingUI.Visible = false;
		_storageWindow.Visible = false;

		RefreshUI();
	}

	public override void _Process(double delta)
	{
		if (_draggedStack != null)
		{
			var mousePos = GetViewport().GetMousePosition();
			_cursorItem.GlobalPosition = mousePos + new Vector2(6, 6);
		}
	}

	public void OpenInventoryUI()
	{
		CloseCraftingUI();
		_inventoryRoot.Visible = true;
	}

	public void CloseInventoryUI()
	{
		if (_draggedStack != null) DropStack();

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
	}

	private void CloseStorageUI()
	{
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

			if (_draggedStack != null && IsCursorOutsideInventory()) DropStack();
		}

		if (e.IsActionPressed("ui_cancel"))
		{
			if (WindowOpen)
			{
				_inventoryRoot.Visible = false;
				_craftingUI.Visible = false;
				_tooltip.Visible = false;
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
			slot.AddThemeStyleboxOverride("panel", _slotStyle);
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
			slot.AddThemeStyleboxOverride("panel", _slotStyle);
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
			slot.AddThemeStyleboxOverride("panel", _slotStyle);
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

	private ItemStack GetStack(bool isHotbar, int index)
	{
		return isHotbar ? _hotbar.GetSlot(index) : _inventory.GetSlot(index);
	}

	private void SetStack(bool isHotbar, int index, ItemStack stack)
	{
		if (isHotbar)
			_hotbar.SetSlot(index, stack);
		else
			_inventory.SetSlot(index, stack);
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

			if (i == _hotbar.SelectedSlot)
			{
				slot.AddThemeStyleboxOverride("panel", _slotHighlightStyle);

				var tween = CreateTween();
				tween.TweenProperty(slot, "scale", new Vector2(1.1f, 1.1f), 0.1f).SetTrans(Tween.TransitionType.Back)
					.SetEase(Tween.EaseType.Out);
			}
			else
			{
				slot.AddThemeStyleboxOverride("panel", _slotStyle);
			}
		}
	}

	private void OnSlotLeftClick(IItemContainer container, int index)
	{
		_draggedStack = InventoryManager.Instance.LeftClick(container, index, _draggedStack);
		UpdateCursor();
		RefreshUI();
	}

	private void OnSlotShiftLeftClick(IItemContainer source, int index)
	{
		// From storage → hotbar, then inventory
		if (source == _storage)
		{
			InventoryManager.Instance.ShiftClick(source, index, _hotbar, _inventory);
		}

		// From player → storage (if open)
		else if (_storage != null)
		{
			InventoryManager.Instance.ShiftClick(source, index, _storage);
		}

		// No storage open → fallback (hotbar ↔ inventory)
		else
		{
			IItemContainer target;
			if (ReferenceEquals(source, _hotbar))
				target = _inventory;
			else
				target = _hotbar;

			InventoryManager.Instance.ShiftClick(source, index, target);
		}

		RefreshUI();
	}

	private void OnSlotRightClick(IItemContainer container, int index)
	{
		_draggedStack = InventoryManager.Instance.RightClick(container, index, _draggedStack);
		UpdateCursor();
		RefreshUI();
	}


	private void DropStack()
	{
		InventoryManager.Instance.DropItem(_player, _draggedStack.Item, _draggedStack.Count);
		_draggedStack = null;
		UpdateCursor();
		RefreshUI();
	}

	private void UpdateCursor()
	{
		if (_draggedStack == null)
		{
			_cursorItem.Visible = false;
			return;
		}

		_cursorItem.Visible = true;
		_cursorIcon.Texture = _draggedStack.Item.Icon;
		_cursorCount.Text = _draggedStack.Count > 1 ? _draggedStack.Count.ToString() : "";
	}

	private bool IsCursorOutsideInventory()
	{
		if (!_inventoryRoot.Visible)
			return false;


		var mousePos = GetViewport().GetMousePosition();
		return !_inventoryWindow.GetGlobalRect().HasPoint(mousePos);
	}
}