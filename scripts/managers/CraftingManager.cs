using Godot;
using System;

public partial class CraftingManager : Node
{
    public static CraftingManager Instance { get; protected set; }

    public override void _Ready()
    {
        Instance = this;
    }

    public bool CanCraft(Player player, CraftingRecipe recipe)
    {
        int totalAvailable = 0;

        foreach (var ingredient in recipe.Ingredients)
        {
            Item item = ItemRegistry.GetItem(ingredient.Key);
            int required = ingredient.Value;

            totalAvailable = InventoryManager.Instance.GetItemTotalCount(item, player.Inventory, player.Hotbar);

            if (totalAvailable < required)
                return false;
        }

        return true;
    }

    public bool CraftItem(Player player, string resultItemId)
    {
        var recipe = CraftingRegistry.GetRecipe(resultItemId);

        if (recipe == null || !CanCraft(player, recipe))
            return false;

        foreach (var ingredient in recipe.Ingredients)
        {
            Item item = ItemRegistry.GetItem(ingredient.Key);
            int required = ingredient.Value;

            int leftover = InventoryManager.Instance.RemoveItem(player, item, required);
            if (leftover > 0)
                return false; // fallback if CanCraft fails for some reason
        }

        InventoryManager.Instance.AddItem(player, ItemRegistry.GetItem(recipe.ResultItemId), recipe.ResultCount);
        return true;
    }

    public void ConsumeIngredients(Player player, CraftingRecipe recipe)
    {
        foreach (var ingredient in recipe.Ingredients)
        {
            Item item = ItemRegistry.GetItem(ingredient.Key);
            int required = ingredient.Value;

            InventoryManager.Instance.RemoveItem(player, item, required);
        }
    }
}
