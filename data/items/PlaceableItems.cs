using Godot;

public static class PlaceableItems
{
    public static void Register()
    {
        ItemRegistry.RegisterItem(new PlaceableItem
        {
            Id = "campfire",
            DisplayName = "Campfire",
            Description = "A small campfire to keep you warm. Can also be used for cooking and to sleep.",
            Icon = LoadIcon("campfire"),
            StackSize = 1,
            PlaceableObjectTypeId = WorldObjectRegistry.StableHash("campfire"),
            PreviewScene = GD.Load<PackedScene>("res://assets/meshes/campfire/Campfire.glb"),
        });

        ItemRegistry.RegisterItem(new PlaceableItem
        {
            Id = "crafting_table",
            DisplayName = "Crafting Table",
            Description = "A basic crafting table for crafting simple items and tools.",
            Icon = LoadIcon("crafting_table"),
            StackSize = 1,
            PlaceableObjectTypeId = WorldObjectRegistry.StableHash("crafting_table"),
            PreviewScene = GD.Load<PackedScene>("res://assets/meshes/crafting-table/CraftingTable.glb"),
        });

        ItemRegistry.RegisterItem(new PlaceableItem
        {
            Id = "kiln",
            DisplayName = "Kiln",
            Description = "A kiln for smelting ores.",
            Icon = LoadIcon("kiln"),
            StackSize = 1,
            PlaceableObjectTypeId = WorldObjectRegistry.StableHash("kiln"),
            PreviewScene = GD.Load<PackedScene>("res://assets/meshes/kiln/Kiln.glb"),
        });

        ItemRegistry.RegisterItem(new PlaceableItem
        {
            Id = "chest",
            DisplayName = "Chest",
            Description = "A chest for storing items.",
            Icon = LoadIcon("chest_one"),
            StackSize = 1,
            PlaceableObjectTypeId = WorldObjectRegistry.StableHash("chest"),
            PreviewScene = GD.Load<PackedScene>("res://assets/meshes/chest/ChestOne.fbx"),
        });
    }

    private static Texture2D LoadIcon(string name) =>
        GD.Load<Texture2D>($"res://assets/icons/{name}.png");

    private static PackedScene LoadScene(string scene) =>
        GD.Load<PackedScene>($"res://scenes/placeables/{scene}.tscn");
}
