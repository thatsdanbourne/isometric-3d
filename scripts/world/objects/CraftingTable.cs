using Godot;

public partial class CraftingTable : InteractableObject, ICraftingStation
{
	public string Label => "Crafting Table";
	public StationType StationType => StationType.CraftingTable;
	public Vector2I TileCoord => Data.TileCoord;

	public override void Interact(Player player)
	{
		player.HUD.OpenCraftingUI(this);
	}

	public override void _Ready()
	{
		InteractPrompt = GetNode<InteractionPrompt>("InteractionPrompt");
	}
}