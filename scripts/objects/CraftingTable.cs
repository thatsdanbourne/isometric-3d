using Godot;

public partial class CraftingTable : WorldObject, IInteractable, ICraftingStation
{
	private InteractionPrompt _interactPrompt;

	public string Label => "Crafting Table";
	public StationType StationType => StationType.CraftingTable;
	public Vector2I TileCoord => Data.TileCoord;

	public void OnFocusGained()
	{
		_interactPrompt.ShowIcon();
	}

	public void OnFocusLost()
	{
		_interactPrompt.HideIcon();
	}

	public bool CanInteract(Player player)
	{
		return true;
	}

	public void Interact(Player player)
	{
		player.HUD.OpenCraftingUI(this);
	}

	public override void _Ready()
	{
		_interactPrompt = GetNode<InteractionPrompt>("InteractionPrompt");
	}
}