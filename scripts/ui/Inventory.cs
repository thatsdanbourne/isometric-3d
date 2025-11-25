using Godot;
using System.Collections.Generic;

public partial class Inventory : Node
{
    [Signal] public delegate void InventoryChangedEventHandler();

    public int SlotCount { get; private set; } = 27;
    public ItemStack[] Slots;

    public Inventory()
    {
        Slots = new ItemStack[SlotCount];
    }

    public ItemStack GetSlot(int index)
    {
        if (index < 0 || index >= SlotCount)
            return null;

        return Slots[index];
    }

    public void SetSlot(int index, ItemStack stack)
    {
        if (index < 0 || index >= SlotCount)
            return;

        Slots[index] = stack;
        EmitSignal(SignalName.InventoryChanged);
    }

    public bool AddItem(Item item, int amount = 1)
    {
        if (item == null || amount <= 0)
            return false;
        
        int remaining = amount;

        for (int i = 0; i < SlotCount && remaining > 0; i++)
        {
            var stack = Slots[i];
            if (stack != null && stack.Item == item && !stack.IsFull)
            {
                remaining = stack.Add(remaining);
            }
        }

        for (int i = 0; i < SlotCount && remaining > 0; i++)
        {
            if (Slots[i] == null)
            {
                int toPlace = Mathf.Min(item.StackSize, remaining);
                Slots[i] = new ItemStack(item, toPlace);
                remaining -= toPlace;
            }
        }

        bool success = remaining < amount;
        if (success)
            EmitSignal(SignalName.InventoryChanged);

        return success;
    }

    public int AddOrMerge(Item item, int amount)
    {
        int remaining = amount;

        for (int i = 0; i < SlotCount && remaining > 0; i++)
        {
            var stack = Slots[i];
            if (stack != null && stack.Item == item && !stack.IsFull)
                remaining = stack.Add(remaining);
        }

        for (int i = 0; i < SlotCount && remaining > 0; i++)
        {
            if (Slots[i] == null)
            {
                int toPlace = Mathf.Min(item.StackSize, remaining);
                Slots[i] = new ItemStack(item, toPlace);
                remaining -= toPlace;
            }
        }

        EmitSignal(SignalName.InventoryChanged);
        return remaining;
    }

    public int RemoveItem(Item item, int amount)
    {
        for (int i = 0; i < SlotCount && amount > 0; i++)
        {
            var slot = Slots[i];
            if (slot != null && slot.Item == item)
            {
                int take = Mathf.Min(slot.Count, amount);
                slot.Count -= take;
                amount -= take;

                if (slot.Count <= 0)
                    Slots[i] = null;
                
                EmitSignal(SignalName.InventoryChanged);
            }
        }

        return amount;
    }

    public int GetItemCount(Item item)
    {
        if (item == null) return 0;

        int count = 0;
        for (int i = 0; i < SlotCount; i++)
        {
            var stack = Slots[i];
            if (stack != null && stack.Item == item)
            {
                count += stack.Count;
            }
        }

        return count;
    }
}
