using Godot;
using System;

public partial class Kiln : StationObject, ICraftingStation
{
	public string Label => "Kiln";
	public StationType StationType => StationType.Kiln;

	private CraftingRecipe activeRecipe;
	private float timeRemaining;
	private int completedCount;
	private int totalCount;
	private bool isCrafting;

	public bool IsCrafting => isCrafting;
	public bool IsTimed => true;
	public int CompletedCount => completedCount;
	public int TotalCount => totalCount;

	public float GetProgress()
	{
		if (activeRecipe == null) return 0f;
		return 1f - (timeRemaining / activeRecipe.CraftTime);
	}

	public CraftingRecipe GetActiveRecipe() => activeRecipe;

	public void StartCraft(CraftingRecipe recipe, Player player)
	{
		if (activeRecipe == null)
		{
			activeRecipe = recipe;
			totalCount = 0;
			completedCount = 0;
			timeRemaining = recipe.CraftTime;
			isCrafting = true;
			SetProcess(true);
		}

		if (activeRecipe != recipe)
			return;

		// Consume ingredients per item added
		CraftingManager.Instance.ConsumeIngredients(player, recipe);
		totalCount += 1;
	}

	public override void _Process(double delta)
	{
		if (!isCrafting) return;

		timeRemaining -= (float)delta;

		if (timeRemaining > 0f) return;

		completedCount++;

		if (completedCount < totalCount)
			timeRemaining = activeRecipe.CraftTime;
		else
		{
			isCrafting = false;
			timeRemaining = 0f;
			SetProcess(false);
		}
	}

	public void CollectOutput(Player player)
	{
		if (completedCount <= 0 || activeRecipe == null) return;

		InventoryManager.Instance.AddItem(player, ItemRegistry.GetItem(activeRecipe.ResultItemId), completedCount);
		totalCount -= completedCount;
		completedCount = 0;

		if (!isCrafting)
		{
			activeRecipe = null;
			totalCount = 0;
		}
	}
}
