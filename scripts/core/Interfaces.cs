using System.Collections.Generic;

public interface IItemContainer
{
    string Label { get; }
    int SlotCount { get; }

    ItemStack GetSlot(int index);
    ItemStack[] GetSlots();
    void SetSlot(int index, ItemStack stack);
}

public interface IInteractable
{
    void OnFocusGained();
    void OnFocusLost();
    void Interact(Player player);

    T GetCapability<T>() where T : class;
}

public interface ICraftingStation
{
    string Label { get; }
    StationType StationType { get; }

    bool IsCrafting { get; }
    bool IsTimed { get; }

    int CompletedCount { get; }
    int TotalCount { get; }

    void StartCraft(CraftingRecipe recipe, Player player);
    float GetProgress();
    CraftingRecipe GetActiveRecipe();
    void CollectOutput(Player player);
}

public interface IChunkStateful<TState>
{
    TState CaptureState();
    void RestoreState(TState state);
}