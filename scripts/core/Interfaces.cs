public interface IItemContainer
{
    int SlotCount { get; }

    ItemStack GetSlot(int index);
    void SetSlot(int index, ItemStack stack);
}

public interface IInteractable
{
    void OnFocusGained();
    void OnFocusLost();
    void Interact(Player player);
}