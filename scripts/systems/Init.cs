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
		MobRegistry.Instance.Register("deer", "res://scenes/mobs/Deer.tscn");
		MobRegistry.Instance.Register("bandit", "res://scenes/mobs/Bandit.tscn");
		GD.Print("Registries loaded! 🔥");
	}
}