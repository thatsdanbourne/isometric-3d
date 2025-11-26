using System;

public interface IItemContainer
{
    int SlotCount { get; }

    ItemStack GetSlot(int index);
    void SetSlot(int index, ItemStack stack);
}
