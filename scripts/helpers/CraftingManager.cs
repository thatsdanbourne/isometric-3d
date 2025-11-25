using Godot;
using System.Collections.Generic;

public partial class CraftingManager : Node
{
    public static CraftingManager Instance { get; protected set; }

    public override void _Ready()
    {
        Instance = this;
    }

    public bool CanCraft(Inventory inv, CraftingRecipe recipe)
    {
        foreach (var key in recipe.Ingredients.Keys)
        {
            int required = recipe.Ingredients[key];
            int available = inv.GetItemCount(key);

            if (available < required)
                return false;   
        }

        return true;
    }

    public bool CraftItem(Inventory inv, CraftingRecipe recipe)
    {
        if (!CanCraft(inv, recipe))
            return false;
        
        foreach (var key in recipe.Ingredients.Keys)
        {
            int required = recipe.Ingredients[key];
            inv.RemoveItem(key, required);
        }

        inv.AddItem(recipe.ResultItem, recipe.ResultCount);
        return true;
    }
}
