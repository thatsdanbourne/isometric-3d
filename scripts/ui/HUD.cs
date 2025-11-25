using Godot;
using System.Collections.Generic;

public partial class HUD : CanvasLayer
{
	private PackedScene pickupScene = ResourceLoader.Load<PackedScene>("res://scenes/ItemPickup.tscn");
	public PackedScene slotPanelScene = ResourceLoader.Load<PackedScene>("res://scenes/ui/HotbarSlot.tscn");

	private StyleBoxFlat slotStyle = ResourceLoader.Load<StyleBoxFlat>("res://scenes/ui/HotbarSlotStyle.tres");
	private StyleBoxFlat slotHighlightStyle = ResourceLoader.Load<StyleBoxFlat>("res://scenes/ui/HotbarSlotHighlight.tres");

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

	private Control craftingRoot;

	private List<HotbarSlot> hotbarSlots = new List<HotbarSlot>();
	private List<HotbarSlot> inventorySlots = new List<HotbarSlot>();

	public bool IsInventoryOpen => inventoryRoot.Visible;

	public override void _Ready()
    {
		cursorItem = GetNode<Control>("CursorItem");
		cursorIcon = cursorItem.GetNode<TextureRect>("Icon");
		cursorCount = cursorItem.GetNode<Label>("Label");

		var player = GetNode<Player>("/root/Game/World/WorldObjects/Player");
        inventory = player.GetNode<Inventory>("Inventory");
		hotbar = player.GetNode<Hotbar>("Hotbar");

		inventoryRoot = GetNode<Control>("Inventory");
		inventoryWindow = inventoryRoot.GetNode<PanelContainer>("InventoryWindow");
		slotGrid = inventoryWindow.GetNode<GridContainer>("MarginContainer/SlotGrid");
		hotbarBox = GetNode<HBoxContainer>("MarginContainer/Hotbar");

		craftingRoot = GetNode<Control>("Crafting");

		BuildInventorySlots();
		BuildHotbarSlots();
		UpdateHotbarHighlight();
		
		inventory.InventoryChanged += RefreshUI;
		hotbar.HotbarChanged += RefreshUI;
		hotbar.SelectedSlotChanged += OnHotbarSelectionChanged;
		
		inventoryRoot.Visible = false;
		craftingRoot.Visible = false;

		GetWindow().ContentScaleFactor = 1.25f;
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

    public override void _UnhandledInput(InputEvent e)
    {
		if (draggedStack != null && IsCursorOutsideInventory() && e is InputEventMouseButton mb && mb.Pressed)
        {
			DropStack();
        }
		
       	if (e.IsActionPressed("toggle_inventory"))
        {
			if(inventoryRoot.Visible && draggedStack != null)
            {
                DropStack();
				cursorItem.Visible = false;
            }
			
			craftingRoot.Visible = false;
            inventoryRoot.Visible = !inventoryRoot.Visible;
        }

		if (e.IsActionPressed("toggle_crafting"))
        {
            inventoryRoot.Visible = false;
			craftingRoot.Visible = !craftingRoot.Visible;
        }
    }

	private void ClearChildren(Node parent)
    {
        while(parent.GetChildCount() > 0)
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
			var slot = slotPanelScene.Instantiate<HotbarSlot>();
			slot.AddThemeStyleboxOverride("panel", slotStyle);
			slot.IsHotbar = false;
			slot.Index = i;
			slot.Hud = this;

			slotGrid.AddChild(slot);
			inventorySlots.Add(slot);
        }
    }

	private void BuildHotbarSlots()
    {
		ClearChildren(hotbarBox);
		hotbarSlots.Clear();

		for (int i = 0; i < hotbar.HotbarSize; i++)
        {
            var slot = slotPanelScene.Instantiate<HotbarSlot>();
			slot.AddThemeStyleboxOverride("panel", slotStyle);
			slot.IsHotbar = true;
			slot.Index = i;
			slot.Hud = this;

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
        for (int i = 0; i < inventory.SlotCount; i++)
        {
            var slot = inventorySlots[i];
			var stack = inventory.GetSlot(i);

			var icon = slot.GetNode<TextureRect>("Icon");
			var countLabel = slot.GetNode<Label>("Label");

			if (stack == null)
            {
                icon.Texture = null;
				countLabel.Text = "";
			}
			else
			{
				icon.Texture = stack.Item.Icon;
				countLabel.Text = stack.Count > 1 ? stack.Count.ToString() : "";
            }
        }

		for (int i = 0; i < hotbar.HotbarSize; i++)
		{
			var slot = hotbarSlots[i];
			var stack = hotbar.GetSlot(i);

			var icon = slot.GetNode<TextureRect>("Icon");
			var countLabel = slot.GetNode<Label>("Label");

			if (stack == null)
			{
				icon.Texture = null;
				countLabel.Text = "";
			}
			else
			{
				icon.Texture = stack.Item.Icon;
				countLabel.Text = stack.Count > 1 ? stack.Count.ToString() : "";
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
            }
			else
			{
				slot.AddThemeStyleboxOverride("panel", slotStyle);
			}
        }
	}

	public void OnSlotLeftClick(bool isHotbar, int index)
	{
		var stack = GetStack(isHotbar, index);

		if (Input.IsKeyPressed(Key.Shift))
        {
			if (isHotbar)
				MoveStackToInventory(stack, index);
			else
				MoveStackToHotbar(stack, index);
			
			RefreshUI();
			return;
        }

		if (draggedStack == null && stack != null)
		{
			SetStack(isHotbar, index, null);
			draggedStack = stack;
			cursorItem.Visible = true;
			cursorIcon.Texture = draggedStack.Item.Icon;
			cursorCount.Text = draggedStack.Count > 1 ? draggedStack.Count.ToString() : "";

			RefreshUI();
			return;
		}
		
		if (draggedStack != null)
		{
			if (stack == null)
            {
                SetStack(isHotbar, index, draggedStack);
				draggedStack = null;
				cursorItem.Visible = false;
            }
			else if (stack.Item == draggedStack.Item)
            {
                stack.Count += draggedStack.Count;
				draggedStack = null;
				cursorItem.Visible = false;
            }
			else
            {
                var temp = stack;
				SetStack(isHotbar, index, draggedStack);
				draggedStack = temp;
				cursorIcon.Texture = draggedStack.Item.Icon;
				cursorCount.Text = draggedStack.Count > 1 ? draggedStack.Count.ToString() : "";
            }

			RefreshUI();
		}
	}

	public void OnSlotRightClick(bool isHotbar, int index)
	{
		var stack = GetStack(isHotbar, index);

		if (stack == null && draggedStack != null)
		{
			// Place a single item
			SetStack(isHotbar, index, new ItemStack(draggedStack.Item, 1));
			draggedStack.Count -= 1;
			cursorCount.Text = draggedStack.Count > 1 ? draggedStack.Count.ToString() : "";
			if (draggedStack.Count <= 0) 
			{
				draggedStack = null;
				cursorItem.Visible = false;
			}
		}
		else if (stack != null)
		{
			int half = stack.Count / 2;
			if (half > 0)
			{
				if (draggedStack == null)
				{
					draggedStack = new ItemStack(stack.Item, half);
					cursorItem.Visible = true;
					cursorIcon.Texture = draggedStack.Item.Icon;
					cursorCount.Text = draggedStack.Count > 1 ? draggedStack.Count.ToString() : "";
				}
				else if (draggedStack.Item == stack.Item)
					draggedStack.Count += half;
					cursorCount.Text = draggedStack.Count > 1 ? draggedStack.Count.ToString() : "";

				stack.Count -= half;

			}
		}

		RefreshUI();
	}

	public void MoveStackToInventory(ItemStack stack, int hotbarIndex)
    {
        for (int i = 0; i < inventory.SlotCount; i++)
        {
            var target = inventory.GetSlot(i);
			if (target != null && target.Item == stack.Item)
            {
				target.Count += stack.Count;
				SetStack(true, hotbarIndex, null);
				return;
            }
        }

		for (int i = 0; i < inventory.SlotCount; i++)
		{
			if (inventory.GetSlot(i) == null)
			{
				SetStack(false, i, stack);
				SetStack(true, hotbarIndex, null);
				return;
			}
		}
    }

	public void MoveStackToHotbar(ItemStack stack, int inventoryIndex)
    {
        for (int i = 0; i < hotbar.HotbarSize; i++)
		{
			var target = hotbar.GetSlot(i);
			if (target != null && target.Item == stack.Item)
            {
                target.Count += stack.Count;
				SetStack(false, inventoryIndex, null);
				return;
            }
		}

		for (int i = 0; i < hotbar.HotbarSize; i++)
        {
            if (hotbar.GetSlot(i) == null)
			{
				SetStack(true, i, stack);
				SetStack(false, inventoryIndex, null);
				return;
			}
        }
    }

	private void DropStack()
    {
        var player = GetNode<Player>("/root/Game/World/WorldObjects/Player");

		var drop = pickupScene.Instantiate<Node3D>();
		drop.Set("item", draggedStack.Item);
		drop.Set("count", draggedStack.Count);
		GetTree().CurrentScene.GetNode<Node3D>("World/WorldObjects").AddChild(drop);
		drop.GlobalPosition = player.GlobalPosition + new Vector3(0, 1, 0);

		draggedStack = null;
		cursorItem.Visible = false;

		RefreshUI();
    }

	private bool IsCursorOutsideInventory()
    {
        if (!inventoryRoot.Visible)
			return false;
		

		var mousePos = GetViewport().GetMousePosition();
		return !inventoryWindow.GetGlobalRect().HasPoint(mousePos);
    }
}

