using Godot;

public partial class CraftingManager : Node
{
	public static CraftingManager Instance { get; protected set; }

	public override void _Ready()
	{
		Instance = this;
	}

	public bool CanCraft(Player player, CraftingRecipe recipe)
	{
		var totalAvailable = 0;

		foreach (var ingredient in recipe.Ingredients)
		{
			var item = ItemRegistry.GetItem(ingredient.Key);
			var required = ingredient.Value;

			totalAvailable = InventoryManager.Instance.GetItemTotalCount(item, player.Inventory, player.Hotbar);

			if (totalAvailable < required)
				return false;
		}

		return true;
	}

	// called for instant craft stations like crafting tables
	public bool CraftItem(Player player, string resultItemId)
	{
		var recipe = CraftingRegistry.GetRecipe(resultItemId);

		if (recipe == null || !CanCraft(player, recipe))
			return false;

		ConsumeIngredients(player, recipe);

		InventoryManager.Instance.AddItem(player, ItemRegistry.GetItem(recipe.ResultItemId), recipe.ResultCount);
		return true;
	}

	// called to consume ingredients when starting a craft in timed stations
	public bool ConsumeIngredients(Player player, CraftingRecipe recipe)
	{
		foreach (var ingredient in recipe.Ingredients)
		{
			var item = ItemRegistry.GetItem(ingredient.Key);
			var required = ingredient.Value;

			var leftover = InventoryManager.Instance.RemoveItem(player, item, required);
			if (leftover > 0)
				return false;
		}

		return true;
	}
}