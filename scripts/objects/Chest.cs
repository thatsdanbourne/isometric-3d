using Godot;

public partial class Chest : WorldObject, IItemContainer, IInteractable, IChunkStateful<StorageStateData>
{
	private ItemStack[] slots;

	public string Label => "Chest";

	public int SlotCount => 9;
	public ItemStack[] GetSlots() => slots;
	public ItemStack GetSlot(int index) => slots[index];
	public void SetSlot(int index, ItemStack stack) => slots[index] = stack;

	public override void _Ready()
	{
		base._Ready();
		slots = new ItemStack[SlotCount];
	}

	public StorageStateData CaptureState()
	{
		return new StorageStateData
		{
			ObjectId = Data.Definition.Id,
			TileCoord = Data.TileCoord,
			Slots = slots
		};
	}

	public void RestoreState(StorageStateData stateData)
	{
		for (int i = 0; i < stateData.Slots.Length; i++)
		{
			SetSlot(i, stateData.Slots[i]);
		}
	}

	public void Interact(Player player)
	{
		// player.OpenStorageUI(this);
	}

	public void OnFocusGained() { }
	public void OnFocusLost() { }

	public T GetCapability<T>() where T : class
	{
		return this as T;
	}
}
