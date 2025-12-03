using Godot;

public static class ResourceItems
{
    public static void Register()
    {
        ItemRegistry.RegisterItem(new ResourceItem {
            Id = "wood",
            DisplayName = "Wood",
            ResourceType = "wood",
            Description = "Common resource used for building and crafting.",
            Icon = LoadTexture("wood"),
            StackSize = 99,
        });

        ItemRegistry.RegisterItem(new ResourceItem {
            Id = "stone",
            DisplayName = "Stone",
            ResourceType = "stone",
            Description = "Common resource used for building and crafting.",
            Icon = LoadTexture("stone"),
            StackSize = 99,
        });

        ItemRegistry.RegisterItem(new ResourceItem {
            Id = "coal",
            DisplayName = "Coal",
            ResourceType = "ore",
            Description = "A common ore used as fuel for fires and smelting.",
            Icon = LoadTexture("coal"),
            StackSize = 99,
        });
    }

    private static Texture2D LoadTexture(string name) =>
        GD.Load<Texture2D>($"res://assets/icons/{name}.png");
}
