using Godot;

public partial class Chest : WorldObject, IItemContainer, IInteractable, IChunkStateful<StorageStateData>
{
	[Signal]
	public delegate void ContainerChangedEventHandler();

	public string Label => "Chest";
	public int SlotCount => 9;

	private InteractionPrompt interactPrompt;
	private ItemStack[] slots;

	public StorageStateData CaptureState()
	{
		return new StorageStateData
		{
			ObjectId = Data.Definition.StableId,
			TileCoord = Data.TileCoord,
			Slots = slots
		};
	}

	public void RestoreState(StorageStateData state)
	{
		if (state == null)
			return;

		var restoredSlots = StationUtils.CloneSlots(state.Slots);

		for (var i = 0; i < SlotCount; i++)
			slots[i] = i < restoredSlots.Length ? restoredSlots[i] : null;

		EmitSignal(SignalName.ContainerChanged);
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
		return StationUtils.CloneSlots(slots);
	}

	public ItemStack GetSlot(int index)
	{
		return slots[index]?.Clone();
	}

	public void SetSlot(int index, ItemStack stack)
	{
		slots[index] = stack?.Clone();
		EmitSignal(SignalName.ContainerChanged);
	}

	public override void _Ready()
	{
		base._Ready();
		slots = new ItemStack[SlotCount];
		interactPrompt = GetNode<InteractionPrompt>("InteractionPrompt");
	}
}