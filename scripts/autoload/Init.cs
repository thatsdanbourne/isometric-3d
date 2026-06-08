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
		StructureDataLoader.LoadAllStructures();
		MobRegistry.Instance.Register("deer", "res://scenes/entities/mobs/Deer.tscn");
		MobRegistry.Instance.Register("bandit", "res://scenes/entities/mobs/Bandit.tscn");
		GD.Print("Registries loaded! 🔥");
	}
}