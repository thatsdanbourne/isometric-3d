using Godot;
using System.Collections.Generic;

public partial class HUD : CanvasLayer
{
	private PackedScene pickupScene = ResourceLoader.Load<PackedScene>("res://scenes/ItemPickup.tscn");
	public PackedScene slotPanelScene = ResourceLoader.Load<PackedScene>("res://scenes/ui/HUD/ItemContainerSlot.tscn");

	private StyleBoxFlat slotStyle = ResourceLoader.Load<StyleBoxFlat>("res://resources/ui/ItemContainerSlotStyle.tres");
	private StyleBoxFlat slotHighlightStyle = ResourceLoader.Load<StyleBoxFlat>("res://resources/ui/ItemContainerSlotHighlight.tres");

	private Inventory inventory;
	private Hotbar hotbar;
	private IItemContainer storage;

	private ItemStack draggedStack;
	private Control cursorItem;
	private TextureRect cursorIcon;
	private Label cursorCount;

	private Control inventoryRoot;
	private PanelContainer inventoryWindow;
	private GridContainer inventorySlotGrid;
	private PanelContainer storageWindow;
	private GridContainer storageSlotGrid;
	private Label storageLabel;

	private HBoxContainer hotbarBox;

	private CraftingUI craftingUI;

	private Tooltip tooltipManager;
	private PanelContainer tooltip;

	private List<ItemContainerSlot> hotbarSlots = new List<ItemContainerSlot>();
	private List<ItemContainerSlot> inventorySlots = new List<ItemContainerSlot>();
	private List<ItemContainerSlot> storageSlots = new List<ItemContainerSlot>();

	private Player player;

	public bool WindowOpen => inventoryRoot.Visible || craftingUI.Visible;
	public bool isCraftingOpen => craftingUI.Visible;
	public bool isInventoryOpen => inventoryRoot.Visible;

	public override void _Ready()
	{
		cursorItem = GetNode<Control>("CursorItem");
		cursorIcon = cursorItem.GetNode<TextureRect>("Icon");
		cursorCount = cursorItem.GetNode<Label>("Label");

		GameManager.Instance.LocalPlayerChanged += (Player p) =>
		{
			player = p;
			player.PlayerReady += OnPlayerReady;
		};

		GetWindow().ContentScaleFactor = 1.25f;
	}

	private void OnPlayerReady()
	{
		inventory = player.GetNode<Inventory>("Inventory");
		hotbar = player.GetNode<Hotbar>("Hotbar");

		inventoryRoot = GetNode<Control>("Inventory");
		inventoryWindow = inventoryRoot.GetNode<PanelContainer>("HBoxContainer/InventoryWindow");
		inventorySlotGrid = inventoryWindow.GetNode<GridContainer>("MarginContainer/SlotGrid");
		storageWindow = inventoryRoot.GetNode<PanelContainer>("HBoxContainer/StorageWindow");
		storageSlotGrid = storageWindow.GetNode<GridContainer>("MarginContainer/SlotGrid");
		storageLabel = storageWindow.GetNode<Label>("Label");

		hotbarBox = GetNode<HBoxContainer>("MarginContainer/Hotbar");

		craftingUI = GetNode<CraftingUI>("Crafting");

		tooltipManager = GetNode<Tooltip>("TooltipManager");
		tooltip = tooltipManager.GetNode<PanelContainer>("Tooltip");

		BuildInventorySlots();
		BuildHotbarSlots();
		UpdateHotbarHighlight();

		inventory.ContainerChanged += RefreshUI;
		hotbar.ContainerChanged += RefreshUI;
		hotbar.SelectedSlotChanged += OnHotbarSelectionChanged;

		inventoryRoot.Visible = false;
		craftingUI.Visible = false;

		RefreshUI();
	}

	public override void _Process(double delta)
	{
		if (draggedStack != null)
		{
			var mousePos = GetViewport().GetMousePosition();
			cursorItem.GlobalPosition = mousePos + new Vector2(6, 6);
		}
	}

	public void OpenInventoryUI()
	{
		CloseCraftingUI();
		inventoryRoot.Visible = true;
	}

	public void CloseInventoryUI()
	{
		if (draggedStack != null)
		{
			DropStack();
		}

		inventoryRoot.Visible = false;
		craftingUI.Visible = false;
		tooltip.Visible = false;

		CloseStorageUI();
	}

	public void OpenStorageUI(IItemContainer storage)
	{
		this.storage = storage;
		OpenInventoryUI();

		BuildStorageSlots(storage);
		storageWindow.Visible = true;
	}

	public void CloseStorageUI()
	{
		storage = null;
		storageWindow.Visible = false;
		ClearChildren(storageSlotGrid);
		storageSlots.Clear();
	}

	public void OpenCraftingUI(ICraftingStation station = null)
	{
		CloseInventoryUI();
		craftingUI.OpenForStation(station);
		craftingUI.Visible = true;
	}

	public void CloseCraftingUI()
	{
		craftingUI.Visible = false;
		inventoryRoot.Visible = false;
		tooltip.Visible = false;
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (e is InputEventMouseButton mb && mb.Pressed)
		{
			if (mb.ButtonIndex == MouseButton.WheelUp)
				hotbar.SelectPrev();
			else if (mb.ButtonIndex == MouseButton.WheelDown)
				hotbar.SelectNext();

			if (draggedStack != null && IsCursorOutsideInventory())
			{
				DropStack();
			}
		}

		if (e.IsActionPressed("ui_cancel"))
		{
			if (WindowOpen)
			{
				inventoryRoot.Visible = false;
				craftingUI.Visible = false;
				tooltip.Visible = false;
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

		for (int i = 0; i < 9; i++)
		{
			if (e.IsActionPressed($"hotbar_{i + 1}"))
			{
				hotbar.SelectSlot(i);
			}
		}
	}

	private void ClearChildren(Node parent)
	{
		foreach (var child in parent.GetChildren())
		{
			if (child is GodotObject go && GodotObject.IsInstanceValid(go))
				go.Free();
		}
	}

	private void BuildInventorySlots()
	{
		ClearChildren(inventorySlotGrid);
		inventorySlots.Clear();

		for (int i = 0; i < inventory.SlotCount; i++)
		{
			var slot = slotPanelScene.Instantiate<ItemContainerSlot>();
			slot.AddThemeStyleboxOverride("panel", slotStyle);
			slot.SetSlot(inventory, i);
			slot.SetStack(inventory[i]);
			slot.SlotLeftClicked += OnSlotLeftClick;
			slot.SlotRightClicked += OnSlotRightClick;
			slot.SlotShiftClicked += OnSlotShiftLeftClick;

			inventorySlotGrid.AddChild(slot);
			inventorySlots.Add(slot);
		}
	}

	private void BuildHotbarSlots()
	{
		ClearChildren(hotbarBox);
		hotbarSlots.Clear();

		for (int i = 0; i < hotbar.SlotCount; i++)
		{
			var slot = slotPanelScene.Instantiate<ItemContainerSlot>();
			slot.AddThemeStyleboxOverride("panel", slotStyle);
			slot.SetSlot(hotbar, i);
			slot.SetStack(hotbar[i]);
			slot.SlotLeftClicked += OnSlotLeftClick;
			slot.SlotRightClicked += OnSlotRightClick;
			slot.SlotShiftClicked += OnSlotShiftLeftClick;

			hotbarBox.AddChild(slot);
			hotbarSlots.Add(slot);
		}
	}

	private void BuildStorageSlots(IItemContainer storage)
	{
		ClearChildren(storageSlotGrid);
		storageSlots.Clear();

		for (int i = 0; i < storage.SlotCount; i++)
		{
			var slot = slotPanelScene.Instantiate<ItemContainerSlot>();
			slot.AddThemeStyleboxOverride("panel", slotStyle);
			slot.SetSlot(storage, i);
			slot.SetStack(storage.GetSlot(i));
			slot.SlotLeftClicked += OnSlotLeftClick;
			slot.SlotRightClicked += OnSlotRightClick;
			slot.SlotShiftClicked += OnSlotShiftLeftClick;

			storageSlotGrid.AddChild(slot);
			storageSlots.Add(slot);
		}

		storageSlotGrid.Columns = Mathf.CeilToInt(storage.SlotCount / 3.0f);
		storageLabel.Text = storage.Label;

		RefreshUI();
	}

	public ItemStack GetStack(bool isHotbar, int index)
	{
		return isHotbar ? hotbar.GetSlot(index) : inventory.GetSlot(index);
	}

	public void SetStack(bool isHotbar, int index, ItemStack stack)
	{
		if (isHotbar)
			hotbar.SetSlot(index, stack);
		else
			inventory.SetSlot(index, stack);
	}

	public void RefreshUI()
	{
		if (inventory == null || hotbar == null) return;

		for (int i = 0; i < inventory.SlotCount; i++)
		{
			inventorySlots[i].SetStack(inventory[i]);
		}

		for (int i = 0; i < hotbar.SlotCount; i++)
		{
			hotbarSlots[i].SetStack(hotbar[i]);
		}

		if (storage != null)
		{
			for (int i = 0; i < storage.SlotCount; i++)
			{
				storageSlots[i].SetStack(storage.GetSlot(i));
			}
		}
	}

	private void OnHotbarSelectionChanged(int selectedIndex)
	{
		UpdateHotbarHighlight();
	}

	private void UpdateHotbarHighlight()
	{
		for (int i = 0; i < hotbarSlots.Count; i++)
		{
			var slot = hotbarSlots[i];

			if (i == hotbar.SelectedSlot)
			{
				slot.AddThemeStyleboxOverride("panel", slotHighlightStyle);

				var tween = CreateTween();
				tween.TweenProperty(slot, "scale", new Vector2(1.1f, 1.1f), 0.1f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
			}
			else
			{
				slot.AddThemeStyleboxOverride("panel", slotStyle);
			}
		}
	}

	public void OnSlotLeftClick(IItemContainer container, int index)
	{
		draggedStack = InventoryManager.Instance.LeftClick(container, index, draggedStack);
		UpdateCursor();
		RefreshUI();
	}

	public void OnSlotShiftLeftClick(IItemContainer source, int index)
	{
		// From storage → hotbar, then inventory
		if (source == storage)
			InventoryManager.Instance.ShiftClick(source, index, hotbar, inventory);

		// From player → storage (if open)
		else if (storage != null)
			InventoryManager.Instance.ShiftClick(source, index, storage);

		// No storage open → fallback (hotbar ↔ inventory)
		else
		{
			IItemContainer target;
			if (ReferenceEquals(source, hotbar))
				target = inventory;
			else
				target = hotbar;

			InventoryManager.Instance.ShiftClick(source, index, target);
		}

		RefreshUI();
	}

	public void OnSlotRightClick(IItemContainer container, int index)
	{
		draggedStack = InventoryManager.Instance.RightClick(container, index, draggedStack);
		UpdateCursor();
		RefreshUI();
	}


	private void DropStack()
	{
		InventoryManager.Instance.DropItem(player, draggedStack.Item, draggedStack.Count);
		draggedStack = null;
		UpdateCursor();
		RefreshUI();
	}

	private void UpdateCursor()
	{
		if (draggedStack == null)
		{
			cursorItem.Visible = false;
			return;
		}

		cursorItem.Visible = true;
		cursorIcon.Texture = draggedStack.Item.Icon;
		cursorCount.Text = draggedStack.Count > 1 ? draggedStack.Count.ToString() : "";
	}

	private bool IsCursorOutsideInventory()
	{
		if (!inventoryRoot.Visible)
			return false;


		var mousePos = GetViewport().GetMousePosition();
		return !inventoryWindow.GetGlobalRect().HasPoint(mousePos);
	}
}

