public static class CraftingRecipeDataLoader
{
    public static void LoadAllCraftingRecipes()
    {
        ToolRecipes.Register();
        PlaceableRecipes.Register();
        ResourceRecipes.Register();
    }
}
