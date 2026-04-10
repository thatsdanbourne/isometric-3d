using System.Collections.Generic;
using System.Linq;

public static class CraftingRegistry
{
	private static readonly List<CraftingRecipe> Recipes = new();

	public static void RegisterRecipe(CraftingRecipe recipe)
	{
		Recipes.Add(recipe);
	}

	public static IEnumerable<CraftingRecipe> AllRecipes()
	{
		return Recipes;
	}

	public static CraftingRecipe GetRecipe(string id)
	{
		return Recipes.FirstOrDefault(r => r.Id == id);
	}

	public static IEnumerable<CraftingRecipe> GetRecipesByStation(StationType stationType)
	{
		return Recipes.Where(r => r.RequiredStation == stationType);
	}

	public static CraftingRecipe GetRecipeByResultId(string resultItemId)
	{
		return Recipes.FirstOrDefault(r => r.ResultItemId == resultItemId);
	}
}