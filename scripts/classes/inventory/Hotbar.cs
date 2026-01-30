using Godot;

public partial class Hotbar : Node, IItemContainer
{
    [Signal] public delegate void ContainerChangedEventHandler();
    [Signal] public delegate void SelectedSlotChangedEventHandler(int slot);

    public string Label => "Hotbar";

    public int SlotCount { get; private set; } = 9;
    public int SelectedSlot { get; private set; } = 0;

    private ItemStack[] slots;

    public ItemStack this[int index] => slots[index];

    public Hotbar()
    {
        slots = new ItemStack[SlotCount];
    }

    public ItemStack GetSlot(int index) => slots[index];
    public ItemStack[] GetSlots() => slots;

    public void SetSlot(int index, ItemStack stack)
    {
        slots[index] = stack;
        EmitSignal(SignalName.ContainerChanged);
    }

    public int GetItemCount(Item item)
    {
        int count = 0;
        for (int i = 0; i < SlotCount; i++)
        {
            var slot = GetSlot(i);
            if (slot != null && slot.Item == item)
            {
                count += slot.Count;
            }
        }

        return count;
    }

    public void SelectSlot(int index)
    {
        if (index < 0 || index >= SlotCount)
            return;

        SelectedSlot = index;
        EmitSignal(SignalName.SelectedSlotChanged, SelectedSlot);
    }

    public void SelectNext()
    {
        SelectSlot((SelectedSlot + 1) % SlotCount);
    }

    public void SelectPrev()
    {
        SelectSlot((SelectedSlot - 1 + SlotCount) % SlotCount);
    }
}
