public static class ResourceRecipes
{
    public static void Register()
    {
        var copperIngotRecipe = new CraftingRecipe("copper_ingot", 1);
        copperIngotRecipe.Ingredients.Add("copper_ore", 5);
        copperIngotRecipe.Ingredients.Add("coal", 2);
        copperIngotRecipe.RequiredStation = StationType.Kiln;
        copperIngotRecipe.CraftTime = 5f;
        CraftingRegistry.RegisterRecipe(copperIngotRecipe);
    }
}
