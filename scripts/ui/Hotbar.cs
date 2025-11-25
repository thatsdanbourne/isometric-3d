using Godot;

public partial class Hotbar : Node
{
    [Signal] public delegate void HotbarChangedEventHandler();
    [Signal] public delegate void SelectedSlotChangedEventHandler(int slot);

    public int HotbarSize = 9;

    public ItemStack[] Slots;
    public int SelectedSlot { get; private set; } = 0;


    public Hotbar()
    {
        Slots = new ItemStack[HotbarSize];
    }
    
    public ItemStack GetSlot(int index)
    {
        if (index < 0 || index >= HotbarSize)
            return null;

        return Slots[index];
    }

    public void SetSlot(int index, ItemStack stack)
    {
        if (index < 0 || index >= HotbarSize)
            return;

        Slots[index] = stack;
        EmitSignal(SignalName.HotbarChanged);
    }

    public int GetItemCount(Item item)
    {
        int count = 0;

        for (int i = 0; i < HotbarSize; i++)
        {
            var stack = Slots[i];
            if (stack != null && stack.Item == item)
                count += stack.Count;
        }

        return count;
    }

    public int RemoveItem(Item item, int amount)
    {
        for (int i = 0; i < HotbarSize && amount > 0; i++)
        {
            var slot = Slots[i];
            if (slot != null && slot.Item == item)
            {
                int take = Mathf.Min(slot.Count, amount);
                slot.Count -= take;
                amount -= take;

                if (slot.Count <= 0)
                    Slots[i] = null;
                
                EmitSignal(SignalName.HotbarChanged);
            }
        }

        return amount;
    }

    public int AddOrMerge(Item item, int amount)
    {
        int remaining = amount;

        for (int i = 0; i < HotbarSize && remaining > 0; i++)
        {
            var stack = Slots[i];
            if (stack != null && stack.Item == item && !stack.IsFull)
                remaining = stack.Add(remaining);
        }

        for (int i = 0; i < HotbarSize && remaining > 0; i++)
        {
            if (Slots[i] == null)
            {
                int toPlace = Mathf.Min(item.StackSize, remaining);
                Slots[i] = new ItemStack(item, toPlace);
                remaining -= toPlace;
            }
        }

        EmitSignal(SignalName.HotbarChanged);
        return remaining;
    }


    public void SelectSlot(int index)
    {
        if (index < 0 || index >= HotbarSize)
            return;

        SelectedSlot = index;
        EmitSignal(SignalName.SelectedSlotChanged, SelectedSlot);
    }

    public void SelectNext()
    {
        SelectSlot((SelectedSlot + 1) % HotbarSize);
    }

    public void SelectPrev()
    {
        SelectSlot((SelectedSlot - 1 + HotbarSize) % HotbarSize);
    }
}
