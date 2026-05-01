using Godot;

public interface IItemContainer
{
	[Signal]
	public delegate void ContainerChangedEventHandler();

	string Label { get; }
	int SlotCount { get; }

	ItemStack GetSlot(int index);
	ItemStack[] GetSlots();
	void SetSlot(int index, ItemStack stack);
	void BindState(StorageStateData state);
}

public interface IInteractable
{
	void OnFocusGained(Player player);
	void OnFocusLost(Player player);
	void UpdateFocus(Player player);
	bool CanInteract(Player player);
	void Interact(Player player);
}

public interface ICraftingStation
{
	string Label { get; }
	StationType StationType { get; }
	Vector2I TileCoord { get; }
}

public interface IProcessingStation : ICraftingStation
{
	float GetProgress();
	CraftingRecipe GetActiveRecipe();
	StationStateData GetDisplayState();
	void BindState(StationStateData state);
}

public interface IToolHittable
{
	Node3D GetHitRoot();
	ToolHitOutcome ReceiveToolHit(ToolItem tool, float damage, Vector3 fromDirection, Vector3 hitPoint);
	ToolHitOutcome ReceiveToolHitFailed(ToolItem tool, Vector3 fromDirection, Vector3 hitPoint);
	string GetImpactType();
	string GetHitSound(ToolItem tool);
	string GetBreakSound();

	float ModifyIncomingToolDamage(ToolItem tool, float damage, float baseDamage)
	{
		return baseDamage;
	}
}