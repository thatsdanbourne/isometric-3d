using Godot;

public class Item
{
	public string Id { get; set; } = "";
	public string DisplayName { get; set; } = "";
	public Texture2D Icon { get; set; }
	public int StackSize { get; set; } = 99;
	public string Description { get; set; } = "";
	public string SoundSet { get; set; } = "generic";
}