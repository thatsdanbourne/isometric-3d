public static class PlaceableRecipes
{
    public static void Register()
    {
        var craftingTableRecipe = new CraftingRecipe("crafting_table", 1);
        craftingTableRecipe.Ingredients.Add("wood", 10);
        CraftingRegistry.RegisterRecipe(craftingTableRecipe);

        var campfireRecipe = new CraftingRecipe("campfire", 1);
        campfireRecipe.Ingredients.Add("wood", 5);
        campfireRecipe.Ingredients.Add("stone", 3);
        CraftingRegistry.RegisterRecipe(campfireRecipe);

        var kilnRecipe = new CraftingRecipe("kiln", 1);
        kilnRecipe.Ingredients.Add("stone", 8);
        kilnRecipe.RequiredStation = StationType.CraftingTable;
        CraftingRegistry.RegisterRecipe(kilnRecipe);
    }
}
