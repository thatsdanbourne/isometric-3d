using Godot;
using System.Collections.Generic;

public partial class HUD : CanvasLayer
{
	public PackedScene slotPanelScene = ResourceLoader.Load<PackedScene>("res://scenes/ui/HotbarSlot.tscn");
	public StyleBoxFlat slotStyle = ResourceLoader.Load<StyleBoxFlat>("res://scenes/ui/HotbarSlotStyle.tres");

	private Inventory inventory;
	private Hotbar hotbar;

	private ItemStack draggedStack;
	private Control cursorItem;
	private TextureRect cursorIcon;
	private Label cursorCount;

	private Control inventoryRoot;
	private GridContainer slotGrid;
	private HBoxContainer hotbarBox;

	private List<HotbarSlot> hotbarSlots = new List<HotbarSlot>();
	private List<HotbarSlot> inventorySlots = new List<HotbarSlot>();

	public override void _Ready()
    {
		cursorItem = GetNode<Control>("CursorItem");
		cursorIcon = cursorItem.GetNode<TextureRect>("Icon");
		cursorCount = cursorItem.GetNode<Label>("Label");

		var player = GetNode<Player>("/root/Game/World/WorldObjects/Player");
        inventory = player.GetNode<Inventory>("Inventory");
		hotbar = player.GetNode<Hotbar>("Hotbar");

		inventoryRoot = GetNode<Control>("Inventory");
		slotGrid = inventoryRoot.GetNode<GridContainer>("InventoryWindow/MarginContainer/SlotGrid");
		hotbarBox = GetNode<HBoxContainer>("MarginContainer/Hotbar");

		BuildInventorySlots();
		BuildHotbarSlots();
		
		inventory.InventoryChanged += RefreshUI;
		hotbar.HotbarChanged += RefreshUI;
		
		inventoryRoot.Visible = false;
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
       	if (e.IsActionPressed("toggle_inventory"))
        {
            inventoryRoot.Visible = !inventoryRoot.Visible;
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
            var slot = slotGrid.GetChild<Panel>(i);
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
			var slot = hotbarBox.GetChild<Panel>(i);
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

	public void OnSlotLeftClick(bool isHotbar, int index)
	{
		var stack = GetStack(isHotbar, index);

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

	//public void HandleSlotDrop(bool srcHotbar, int srcIndex, bool destHotbar, int destIndex)
	// {
	// 	var srcStack = GetStack(srcHotbar, srcIndex);
	// 	var destStack = GetStack(destHotbar, destIndex);

	// 	if (srcStack == null)
	// 		return;
		
	// 	if (srcHotbar == destHotbar && srcIndex == destIndex)
	// 		return;
		

	// 	SetStack(srcHotbar, srcIndex, destStack);
	// 	SetStack(destHotbar, destIndex, srcStack);

	// 	RefreshUI();
	// }
}

