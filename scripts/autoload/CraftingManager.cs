using Godot;

public partial class CraftingManager : Node
{
	public static CraftingManager Instance { get; private set; }

	public override void _EnterTree()
	{
		Instance = this;
	}

	public bool CanCraft(Player player, CraftingRecipe recipe)
	{
		foreach (var ingredient in recipe.Ingredients)
		{
			var item = ItemRegistry.GetItem(ingredient.Key);
			var required = ingredient.Value;

			var totalAvailable = InventoryManager.Instance.GetItemTotalCount(item, player.Inventory, player.Hotbar);

			if (totalAvailable < required)
				return false;
		}

		return true;
	}

	public void RequestCollect(Player player, ICraftingStation station)
	{
		if (player == null || station == null)
			return;

		var world = GameManager.Instance.CurrentWorld;
		if (world == null)
			return;

		world.Sync.RequestCollectStationOutput(station.TileCoord);
	}

	public void RequestCraft(Player player, CraftingRecipe recipe, ICraftingStation station = null)
	{
		if (player == null || recipe == null)
			return;

		var world = GameManager.Instance.CurrentWorld;
		if (world == null)
			return;

		if (station is IProcessingStation)
		{
			world.Sync.RequestStartStationCraft(station.TileCoord, recipe.Id);
			return;
		}

		world.Sync.RequestCraftItem(recipe.Id);
	}

	public bool ExecuteCraftRequest(Player player, CraftingRecipe recipe, ICraftingStation station = null)
	{
		if (player == null || recipe == null)
			return false;

		if (station != null)
			return false;

		if (!CanCraft(player, recipe))
			return false;

		ConsumeIngredients(player, recipe);
		GiveCraftResult(player, recipe);
		return true;
	}

	private void GiveCraftResult(Player player, CraftingRecipe recipe)
	{
		InventoryManager.Instance.AddItem(
			player,
			ItemRegistry.GetItem(recipe.ResultItemId),
			recipe.ResultCount
		);
	}

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