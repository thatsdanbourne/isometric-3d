using Godot;

[GlobalClass]
public partial class ResourceItem : Item
{
    [Export] public string ResourceType { get; set; }
}
