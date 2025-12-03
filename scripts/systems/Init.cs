using Godot;

public partial class Init : Node
{
    public override void _Ready()
    {
        GD.Print("Loading registries...");
        ItemDataLoader.LoadAllItems();
        CraftingRecipeDataLoader.LoadAllCraftingRecipes();
        WorldObjectRegistry.RegisterDefaults();
        GD.Print("Registries loaded! 🔥");
    }
}
