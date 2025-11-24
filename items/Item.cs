using Godot;

[GlobalClass]
public partial class Item : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public string DisplayName { get; set; } = "";
    [Export] public Texture2D Icon { get; set; }
    [Export] public int StackSize { get; set; } = 99;
    [Export] public string Description { get; set; } = "";
}
