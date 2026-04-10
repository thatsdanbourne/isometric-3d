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
}

public interface IInteractable
{
	void OnFocusGained();
	void OnFocusLost();

	T GetCapability<T>() where T : class;
}

public interface ICraftingStation
{
	string Label { get; }
	StationType StationType { get; }
	Vector2I TileCoord { get; }
}

public interface IProcessingStation : ICraftingStation
{
	bool IsCrafting { get; }
	int CompletedCount { get; }
	int TotalCount { get; }

	float GetProgress();
	CraftingRecipe GetActiveRecipe();
	StationStateData GetDisplayState();
}

public interface IChunkStateful<TState>
{
	TState CaptureState();
	void RestoreState(TState state);
}

public interface IToolHittable
{
	Node3D GetHitRoot();
	ToolHitOutcome ReceiveToolHit(ToolItem tool, float damage, Vector3 fromDirection, Vector3 hitPoint);
	ToolHitOutcome ReceiveToolHitFailed(ToolItem tool, Vector3 fromDirection, Vector3 hitPoint);

	float ModifyIncomingToolDamage(ToolItem tool, float damage, float baseDamage)
	{
		return baseDamage;
	}
}