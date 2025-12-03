using Godot;

public static class PlaceableItems
{
    public static void Register()
    {
        ItemRegistry.RegisterItem(new PlaceableItem {
            Id = "campfire",
            DisplayName = "Campfire",
            Description = "A small campfire to keep you warm. Can also be used for cooking and to sleep.",
            Icon = LoadIcon("campfire"),
            StackSize = 1,
            PlaceableScene = LoadScene("Campfire"),
        });
    }
    
    private static Texture2D LoadIcon(string name) =>
        GD.Load<Texture2D>($"res://assets/icons/{name}.png");

    private static PackedScene LoadScene(string scene) =>
        GD.Load<PackedScene>($"res://scenes/{scene}.tscn");
}
