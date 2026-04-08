using Godot;
using System;

public class StorageStateContainer : IItemContainer
{
	private readonly StorageStateData _state;
	private readonly string _label;

	public string Label => _label;
	public int SlotCount => _state.Slots.Length;

	public StorageStateContainer(StorageStateData state, string label = "Storage")
	{
		_state = state;
		_label = label;
		state.Slots ??= [];
	}

	public ItemStack[] GetSlots()
	{
		return StationUtils.CloneSlots(_state.Slots);
	}

	public ItemStack GetSlot(int index)
	{
		if (index < 0 || index >= SlotCount)
			return null;

		return _state.Slots[index]?.Clone();
	}

	public void SetSlot(int index, ItemStack stack)
	{
		if (index < 0 || index >= SlotCount)
			return;

		_state.Slots[index] = stack?.Clone();
	}

	public StorageStateData CaptureState()
	{
		return new StorageStateData
		{
			ObjectId = _state.ObjectId,
			TileCoord = _state.TileCoord,
			Slots = GetSlots()
		};
	}
}