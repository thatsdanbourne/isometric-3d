using Godot;

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