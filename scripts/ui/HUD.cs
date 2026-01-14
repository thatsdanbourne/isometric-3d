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

	private ItemStack draggedStack;
	private Control cursorItem;
	private TextureRect cursorIcon;
	private Label cursorCount;

	private Control inventoryRoot;
	private PanelContainer inventoryWindow;
	private GridContainer slotGrid;
	private HBoxContainer hotbarBox;

	private CraftingUI craftingUI;

	private Tooltip tooltipManager;
	private PanelContainer tooltip;

	private List<ItemContainerSlot> hotbarSlots = new List<ItemContainerSlot>();
	private List<ItemContainerSlot> inventorySlots = new List<ItemContainerSlot>();

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
		inventoryWindow = inventoryRoot.GetNode<PanelContainer>("InventoryWindow");
		slotGrid = inventoryWindow.GetNode<GridContainer>("MarginContainer/SlotGrid");
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
		if (draggedStack != null && IsCursorOutsideInventory() && e is InputEventMouseButton mb && mb.Pressed)
		{
			DropStack();
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
		while (parent.GetChildCount() > 0)
		{
			parent.GetChild(0).QueueFree();
		}
	}

	private void BuildInventorySlots()
	{
		ClearChildren(slotGrid);
		inventorySlots.Clear();

		for (int i = 0; i < inventory.SlotCount; i++)
		{
			var slot = slotPanelScene.Instantiate<ItemContainerSlot>();
			slot.AddThemeStyleboxOverride("panel", slotStyle);
			slot.IsHotbar = false;
			slot.SetSlot(inventory, i);
			slot.SetStack(inventory[i]);
			slot.SlotLeftClicked += OnSlotLeftClick;
			slot.SlotRightClicked += OnSlotRightClick;
			slot.SlotShiftClicked += OnSlotShiftLeftClick;

			slotGrid.AddChild(slot);
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
			slot.IsHotbar = true;
			slot.SetSlot(hotbar, i);
			slot.SetStack(hotbar[i]);
			slot.SlotLeftClicked += OnSlotLeftClick;
			slot.SlotRightClicked += OnSlotRightClick;
			slot.SlotShiftClicked += OnSlotShiftLeftClick;

			hotbarBox.AddChild(slot);
			hotbarSlots.Add(slot);
		}
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

	public void OnSlotLeftClick(bool isHotbar, int index)
	{
		draggedStack = InventoryManager.Instance.LeftClick(isHotbar, index, draggedStack, inventory, hotbar);
		UpdateCursor();
		RefreshUI();
	}

	public void OnSlotShiftLeftClick(bool isHotbar, int index)
	{
		var remaining = InventoryManager.Instance.ShiftClick(isHotbar, index, inventory, hotbar);
		if (isHotbar)
			hotbar.SetSlot(index, remaining);
		else
			inventory.SetSlot(index, remaining);

		RefreshUI();
	}

	public void OnSlotRightClick(bool isHotbar, int index)
	{
		draggedStack = InventoryManager.Instance.RightClick(isHotbar, index, draggedStack, inventory, hotbar);
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

