using Godot;
using System.Collections.Generic;

public partial class CraftingManager : Node
{
    public static CraftingManager Instance { get; protected set; }

    public override void _Ready()
    {
        Instance = this;
    }

    public bool CanCraft(Inventory inv, Hotbar hotbar, CraftingRecipe recipe)
    {
        foreach (var ingredient in recipe.Ingredients)
        {
            Item item = ingredient.Key;
            int required = ingredient.Value;

            int available = inv.GetItemCount(item) + hotbar.GetItemCount(item);

            if (available < required)
                return false;   
        }

        return true;
    }

    public bool CraftItem(Inventory inv, Hotbar hotbar, CraftingRecipe recipe)
    {
        if (!CanCraft(inv, hotbar, recipe))
            return false;
        
        foreach (var ingredient in recipe.Ingredients)
        {
            Item item = ingredient.Key;
            int required = ingredient.Value;

            required = inv.RemoveItem(item, required);

            if (required > 0)
            {
                hotbar.RemoveItem(item, required);
            }
        }

        inv.AddItem(recipe.ResultItem, recipe.ResultCount);
        return true;
    }
}
