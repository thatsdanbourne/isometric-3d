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
            Item item = ingredient.Key;
            int required = ingredient.Value;

            totalAvailable = player.Inventory.GetItemCount(item) + player.Hotbar.GetItemCount(item);

            if (totalAvailable < required)
                return false;   
        }

        return true;
    }

    public bool CraftItem(Player player, CraftingRecipe recipe)
    {
        if (!CanCraft(player, recipe))
            return false;
        
        foreach (var ingredient in recipe.Ingredients)
        {
            Item item = ingredient.Key;
            int required = ingredient.Value;

           int leftover = InventoryManager.Instance.RemoveItem(player, item, required);
           if (leftover > 0)
               return false; // fallback if CanCraft fails for some reason
        }

        InventoryManager.Instance.AddItem(player, recipe.ResultItem, recipe.ResultCount);
        return true;
    }
}
