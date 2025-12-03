using Godot;
using System.Collections.Generic;
using System.Linq;

public static class CraftingRegistry
{
    public static List<CraftingRecipe> _recipes = new();

    public static void RegisterRecipe(CraftingRecipe recipe)
    {
        _recipes.Add(recipe);
    }

    public static IEnumerable<CraftingRecipe> AllRecipes() => _recipes;

    public static CraftingRecipe GetRecipe(string resultItemId)
    {
        return _recipes.FirstOrDefault(r => r.ResultItemId == resultItemId);
    }
}
