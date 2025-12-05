public static class PlaceableRecipes
{
    public static void Register()
    {
        var campfireRecipe = new CraftingRecipe("campfire", 1);
        campfireRecipe.Ingredients.Add("wood", 5);
        campfireRecipe.Ingredients.Add("stone", 3);
        CraftingRegistry.RegisterRecipe(campfireRecipe);
    }
}
