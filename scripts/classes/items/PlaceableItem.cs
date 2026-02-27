using Godot;

public class PlaceableItem : Item
{
    public int PlaceableObjectTypeId;
    public PackedScene PreviewScene { get; set; }
    
    public WorldObjectDefinition PlaceableObjectDefinition =>
        WorldObjectRegistry.GetDefinition(PlaceableObjectTypeId);
}
