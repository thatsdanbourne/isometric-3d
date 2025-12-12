using Godot;
using System.Collections.Generic;

public partial class CraftingUI : Control
{
	public Player Player;

	private VBoxContainer recipeList;
	private List<RecipeEntry> recipeEntries = new();

	private PackedScene recipeEntryScene =
		GD.Load<PackedScene>("res://scenes/ui/crafting/RecipeEntry.tscn");


	public override void _Ready()
	{
		GameManager.Instance.LocalPlayerChanged += (Player p) =>
		{
			Player = p;

			Player.PlayerReady += () =>
			{
				Player.Inventory.ContainerChanged += RefreshCraftingUI;
				Player.Hotbar.ContainerChanged += RefreshCraftingUI;
				RefreshCraftingUI();
			};
		};

		recipeList = GetNode<VBoxContainer>("CraftingWindow/VBoxContainer/MarginContainer/ScrollContainer/RecipeList");

		BuildRecipeList();
	}

	private void BuildRecipeList()
	{
		foreach (var recipe in CraftingRegistry.AllRecipes())
		{
			var entry = recipeEntryScene.Instantiate<RecipeEntry>();
			entry.SetRecipe(recipe, Player);
			entry.CraftItem += CraftItem;

			recipeEntries.Add(entry);
			recipeList.AddChild(entry);
		}
	}

	private void CraftItem(string resultItemId)
	{
		CraftingManager.Instance.CraftItem(Player, resultItemId);
		RefreshCraftingUI();
	}

	public void RefreshCraftingUI()
	{
		foreach (var entry in recipeEntries)
		{
			var recipe = entry.Recipe;
			var canCraft = CraftingManager.Instance.CanCraft(Player, recipe);

			entry.Result.Modulate = canCraft ? Colors.White : new Color(1, 1, 1, 0.5f);

			entry.Result.HoldToActivate = canCraft;

			entry.Result.AddThemeStyleboxOverride("panel",
				canCraft ? entry.slotHighlightStyle : entry.slotDefaultStyle);

			for (int i = 0; i < entry.IngredientSlots.Count; i++)
			{
				var slot = entry.IngredientSlots[i];
				var ingredientInfo = entry.Ingredients[i];

				int owned = InventoryManager.Instance.GetItemTotalCount(ingredientInfo.Item, Player.Inventory, Player.Hotbar);

				slot.CountLabel.Modulate = owned >= ingredientInfo.RequiredCount ? Colors.White : Colors.Red;
			}
		}
	}
}