using Godot;

public class PlaceableItem : Item
{
    public WorldObjectDefinition PlaceableObjectDefinition { get; set; }
    public PackedScene PreviewScene { get; set; }
}
