using Godot;

public class PlaceableItem : Item
{
    public PackedScene PlaceableScene { get; set; }
    public Texture2D PreviewTexture { get; set; }
    public SpriteFrames PreviewFrames { get; set; }

    public bool IsAnimated => PreviewFrames != null;
}
