using Godot;

public partial class Inventory : Node, IItemContainer
{
	[Signal]
	public delegate void ContainerChangedEventHandler();

	public string Label => "Inventory";

	public int SlotCount { get; private set; } = 27;
	private ItemStack[] slots;

	public ItemStack this[int index] => slots[index];

	public Inventory()
	{
		slots = new ItemStack[SlotCount];
	}

	public void BindState(StorageStateData state)
	{
	}

	public ItemStack GetSlot(int index)
	{
		return slots[index];
	}

	public ItemStack[] GetSlots()
	{
		return slots;
	}

	public void SetSlot(int index, ItemStack stack)
	{
		slots[index] = stack;
		EmitSignal(SignalName.ContainerChanged);
	}

	public int GetItemCount(Item item)
	{
		var count = 0;
		for (var i = 0; i < SlotCount; i++)
		{
			var slot = GetSlot(i);
			if (slot != null && slot.Item == item) count += slot.Count;
		}

		return count;
	}
}