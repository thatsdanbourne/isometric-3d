using Godot;
using System;

public partial class CraftingTable : StationObject, ICraftingStation
{
	public string Label => "Crafting Table";
	public StationType StationType => StationType.CraftingTable;

	public bool IsCrafting => false;
	public bool IsTimed => false;

	public int CompletedCount => 0;
	public int TotalCount => 0;

	public CraftingRecipe GetActiveRecipe() => null;
	public float GetProgress() => 0f;

	public void StartCraft(CraftingRecipe recipe, Player player)
	{
		if (!CraftingManager.Instance.CanCraft(player, recipe))
			return;

		CraftingManager.Instance.CraftItem(player, recipe.ResultItemId);
	}

	public void CollectOutput(Player player)
	{
		// No output to collect in a crafting table
	}
}
