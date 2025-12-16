using Godot;

public class PlaceableItem : Item
{
    public WorldObjectDefinition PlaceableObjectDefinition { get; set; }
    public Texture2D PreviewTexture { get; set; }
    public SpriteFrames PreviewFrames { get; set; }

    public bool IsAnimated => PreviewFrames != null;
}
