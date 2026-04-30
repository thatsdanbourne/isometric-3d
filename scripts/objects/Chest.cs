using Godot;

public partial class Chest : InteractableObject, IItemContainer
{
	[Signal]
	public delegate void ContainerChangedEventHandler();

	private StorageStateData _state;

	public string Label => "Chest";
	public int SlotCount => 9;


	public void BindState(StorageStateData state)
	{
		_state = state;
		EmitSignal(SignalName.ContainerChanged);
	}

	public override void Interact(Player player)
	{
		player.HUD.OpenStorageUI(this);
	}

	public ItemStack[] GetSlots()
	{
		return StationUtils.CloneSlots(_state.Slots);
	}

	public ItemStack GetSlot(int index)
	{
		return _state?.Slots[index]?.Clone();
	}

	public void SetSlot(int index, ItemStack stack)
	{
		_state.Slots[index] = stack?.Clone();
		EmitSignal(SignalName.ContainerChanged);
	}

	public override void _Ready()
	{
		base._Ready();
		InteractPrompt = GetNode<InteractionPrompt>("InteractionPrompt");
	}
}