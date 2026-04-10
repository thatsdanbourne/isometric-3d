using Godot;

public partial class CraftingTable : WorldObject, IInteractable, ICraftingStation
{
	private InteractionPrompt interactPrompt;

	public string Label => "Crafting Table";
	public StationType StationType => StationType.CraftingTable;
	public Vector2I TileCoord => Data.TileCoord;

	public void OnFocusGained()
	{
		interactPrompt.ShowIcon();
	}

	public void OnFocusLost()
	{
		interactPrompt.HideIcon();
	}

	public T GetCapability<T>() where T : class
	{
		return this as T;
	}

	public override void _Ready()
	{
		interactPrompt = GetNode<InteractionPrompt>("InteractionPrompt");
	}
}