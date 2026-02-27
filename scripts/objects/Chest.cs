public partial class Chest : WorldObject, IItemContainer, IInteractable, IChunkStateful<StorageStateData>
{
    public string Label => "Chest";
    public int SlotCount => 9;

    private InteractionPrompt interactPrompt;
    private ItemStack[] slots;

    public StorageStateData CaptureState()
    {
        return new StorageStateData
        {
            Slots = slots
        };
    }

    public void RestoreState(StorageStateData stateData)
    {
        for (var i = 0; i < stateData.Slots.Length; i++) SetSlot(i, stateData.Slots[i]);
    }

    public void OnFocusGained()
    {
        interactPrompt.ShowIcon();
    }

    public void OnFocusLost()
    {
        interactPrompt.HideIcon();
    }

    public T GetCapability<T>() where T : class
    {
        return this as T;
    }

    public ItemStack[] GetSlots()
    {
        return slots;
    }

    public ItemStack GetSlot(int index)
    {
        return slots[index];
    }

    public void SetSlot(int index, ItemStack stack)
    {
        slots[index] = stack;
    }

    public override void _Ready()
    {
        base._Ready();
        slots = new ItemStack[SlotCount];
        interactPrompt = GetNode<InteractionPrompt>("InteractionPrompt");
    }
}