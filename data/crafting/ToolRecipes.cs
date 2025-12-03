public static class ToolRecipes
{
    public static void Register()
    {
        var stoneAxeRecipe = new CraftingRecipe("stone_axe", 1);
        stoneAxeRecipe.Ingredients.Add("wood", 2);
        stoneAxeRecipe.Ingredients.Add("stone", 4);
        stoneAxeRecipe.RequiredStationId = "workbench";
        CraftingRegistry.RegisterRecipe(stoneAxeRecipe);

        var stonePickaxeRecipe = new CraftingRecipe("stone_pickaxe", 1);
        stonePickaxeRecipe.Ingredients.Add("wood", 2);
        stonePickaxeRecipe.Ingredients.Add("stone", 4);
        stonePickaxeRecipe.RequiredStationId = "workbench";
        CraftingRegistry.RegisterRecipe(stonePickaxeRecipe);
    }
}
