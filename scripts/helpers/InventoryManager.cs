using Godot;
using System;

public partial class InventoryManager : Node
{
    public static InventoryManager Instance { get; protected set; }

    public override void _Ready()
    {
        Instance = this;
    }

    // add logic
    public void AddItem(Player player, Item item, int amount)
    {
        var inv = player.Inventory;
        var hotbar = player.Hotbar;

        int remaining = amount;

        // Try to merge into existing stacks in hotbar first
        remaining = MergeStacks(hotbar, item, remaining);

        // Then try to merge into existing stacks in inventory
        remaining = MergeStacks(inv, item, remaining);

        // Then try to add into empty slots in hotbar
        remaining = FillEmptySlots(hotbar, item, remaining);

        // Finally try to add into empty slots in inventory
        remaining = FillEmptySlots(inv, item, remaining);

        if (remaining > 0)
            DropItem(player, item, remaining);

        player.HUD.RefreshUI();
    }


    // remove logic
    public int RemoveItem(Player player, Item item, int amount)
    {
        int remaining = RemoveFromContainer(player.Inventory, item, amount);

        if (remaining > 0)
            remaining = RemoveFromContainer(player.Hotbar, item, remaining);
        
        return remaining;
        
    }

    public int RemoveFromContainer(IItemContainer container, Item item, int remaining)
    {
        for (int i = 0; i < container.SlotCount && remaining > 0; i++)
        {
            var stack = container.GetSlot(i);
            if (stack == null || stack.Item != item) continue;

            int remove = Math.Min(stack.Count, remaining);
            stack.Count -= remove;
            remaining -= remove;

            if (stack.Count <= 0)
                container.SetSlot(i, null);
            else
                container.SetSlot(i, stack);
        }

        return remaining;
    }


    // merge logic
    private int MergeStacks(IItemContainer container, Item item, int remaining)
    {
        for (int i = 0; i < container.SlotCount && remaining > 0; i++)
        {
            var slot = container.GetSlot(i);
            if (slot == null || slot.Item != item) continue;

            int space = slot.Item.StackSize - slot.Count;
            if (space <= 0) continue;

            int toAdd = Mathf.Min(space, remaining);
            slot.Count += toAdd;
            remaining -= toAdd;

            container.SetSlot(i, slot);
        }

        return remaining;
    }


    // fill empty slots
    private int FillEmptySlots(IItemContainer container, Item item, int remaining)
    {
        for (int i = 0; i < container.SlotCount && remaining > 0; i++)
        {
            if (container.GetSlot(i) != null)
                continue;

            int toAdd = Mathf.Min(item.StackSize, remaining);
            container.SetSlot(i, new ItemStack(item, toAdd));
            remaining -= toAdd;
        }

        return remaining;
    }


    // drop logic
    public void DropItem(Player player, Item item, int remaining)
    {
        PackedScene itemScene = ResourceLoader.Load<PackedScene>("res://scenes/ItemPickup.tscn");

        while (remaining > 0)
        {
            int amount = Mathf.Min(item.StackSize, remaining);
            remaining -= amount;

            var drop = itemScene.Instantiate<Node3D>();
            drop.Set("item", item);
            drop.Set("count", amount);

            var world = player.GetParent();
            world.AddChild(drop);
            drop.GlobalPosition = player.GlobalPosition + new Vector3(0, 1, 0);
        }
    }

    // swap logic
    public void SwapStacks(IItemContainer src, int si, IItemContainer dest, int di)
    {
        var srcStack = src.GetSlot(si);
        var destStack = dest.GetSlot(di);

        src.SetSlot(si, destStack);
        dest.SetSlot(di, srcStack);
    }



    // inventory shortcut logic

        public ItemStack LeftClick(
        bool isHotbar,
        int index,
        ItemStack dragged,
        Inventory inventory,
        Hotbar hotbar
    )
    {
        var stack = isHotbar ? hotbar.GetSlot(index) : inventory.GetSlot(index);

        // pick up stack
        if (dragged == null && stack != null)
        {
            Set(isHotbar, index, null, inventory, hotbar);
            return stack;
        }

        // place stack
        if (dragged != null && stack == null)
        {
            Set(isHotbar, index, dragged, inventory, hotbar);
            return null;
        }

        // merge stacks
        if (dragged != null && stack != null && dragged.Item == stack.Item)
        {
            int max = stack.Item.StackSize;
            int space = max - stack.Count;

            if (space > 0)
            {
                int move = Math.Min(space, dragged.Count);
                stack.Count += move;
                dragged.Count -= move;

                if (dragged.Count <= 0)
                    return null;

                return dragged;
            }
        }

        // swap stacks
        if (dragged != null && stack != null && dragged.Item != stack.Item)
        {
            Set(isHotbar, index, dragged, inventory, hotbar);
            return stack;
        }

        return dragged;
    }


    public ItemStack RightClick(
        bool isHotbar,
        int index,
        ItemStack dragged,
        Inventory inventory,
        Hotbar hotbar
    )
    {
        var stack = isHotbar ? hotbar.GetSlot(index) : inventory.GetSlot(index);

        // place one item from dragged stack
        if (dragged != null)
        {
            if (stack == null)
            {
                Set(isHotbar, index, new ItemStack(dragged.Item, 1), inventory, hotbar);
                dragged.Count -= 1;
                return dragged.Count > 0 ? dragged : null;
            }

            if (stack.Item == dragged.Item && stack.Count < stack.Item.StackSize)
            {
                stack.Count += 1;
                dragged.Count -= 1;
                return dragged.Count > 0 ? dragged : null;
            }

            return dragged;
        }

        // pick up half of the stack
        if (dragged == null && stack != null)
        {
            int half = stack.Count / 2;
            if (half <= 0)
                return null;
            
            stack.Count -= half;
            return new ItemStack(stack.Item, half);
        }

        return null;
    }

    public ItemStack ShiftClick(bool fromHotbar, int index, Inventory inventory, Hotbar hotbar)
    {
        var stack = fromHotbar ? hotbar.GetSlot(index) : inventory.GetSlot(index);
        if (stack == null) return null;
        
        int remaining = stack.Count;

        if (fromHotbar)
        {
            remaining = MergeStacks(inventory, stack.Item, remaining);

            if (remaining > 0)
                remaining = FillEmptySlots(inventory, stack.Item, remaining);
        } 
        else
        {
            remaining = MergeStacks(hotbar, stack.Item, remaining);

            if (remaining > 0)
                remaining = FillEmptySlots(hotbar, stack.Item, remaining);
        }

        if (remaining > 0)
            return new ItemStack(stack.Item, remaining);
        else return null;
    }

    private void Set(bool isHotbar, int index, ItemStack stack, Inventory inventory, Hotbar hotbar)
    {
        if (isHotbar)
            hotbar.SetSlot(index, stack);
        else
            inventory.SetSlot(index, stack);
    }

    // helpers
    public int GetItemTotalCount(Item item, Inventory inventory, Hotbar hotbar)
    {
        return inventory.GetItemCount(item) + hotbar.GetItemCount(item);
    }
}
