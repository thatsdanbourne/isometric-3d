public static class ToolRecipes
{
	public static void Register()
	{
		var stoneAxeRecipe = new CraftingRecipe("stone_axe", 1)
		{
			Id = "stone_axe"
		};
		stoneAxeRecipe.Ingredients.Add("stick", 2);
		stoneAxeRecipe.Ingredients.Add("stone", 4);
		CraftingRegistry.RegisterRecipe(stoneAxeRecipe);

		var stonePickaxeRecipe = new CraftingRecipe("stone_pickaxe", 1)
		{
			Id = "stone_pickaxe"
		};
		stonePickaxeRecipe.Ingredients.Add("stick", 2);
		stonePickaxeRecipe.Ingredients.Add("stone", 4);
		CraftingRegistry.RegisterRecipe(stonePickaxeRecipe);

		var copperPickaxeRecipe = new CraftingRecipe("copper_pickaxe", 1)
		{
			Id = "copper_pickaxe"
		};
		copperPickaxeRecipe.Ingredients.Add("stick", 2);
		copperPickaxeRecipe.Ingredients.Add("copper_ingot", 4);
		copperPickaxeRecipe.RequiredStation = StationType.CraftingTable;
		CraftingRegistry.RegisterRecipe(copperPickaxeRecipe);
	}
}