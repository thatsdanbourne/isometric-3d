using Godot;

public partial class Chest : WorldObject, IItemContainer, IInteractable
{
	[Signal]
	public delegate void ContainerChangedEventHandler();

	private StorageStateData _state;

	public string Label => "Chest";
	public int SlotCount => 9;

	private InteractionPrompt _interactPrompt;


	public void BindState(StorageStateData state)
	{
		_state = state;
		EmitSignal(SignalName.ContainerChanged);
	}

	public void OnFocusGained()
	{
		_interactPrompt.ShowIcon();
	}

	public void OnFocusLost()
	{
		_interactPrompt.HideIcon();
	}

	public void Interact(Player player)
	{
		player.HUD.OpenStorageUI(this);
	}

	public bool CanInteract(Player player)
	{
		return true;
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
		_interactPrompt = GetNode<InteractionPrompt>("InteractionPrompt");
	}
}