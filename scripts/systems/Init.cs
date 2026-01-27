using Godot;

public partial class Init : Node
{
    public override void _Ready()
    {
        GD.Print("Loading registries...");
        WorldObjectRegistry.RegisterDefaults();
        ItemDataLoader.LoadAllItems();
        CraftingRecipeDataLoader.LoadAllCraftingRecipes();
        TileDataLoader.LoadAllTiles();
        DetailMeshDataLoader.LoadAllDetailMeshes();
        GD.Print("Registries loaded! 🔥");
    }
}
