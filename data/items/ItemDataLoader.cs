public static class ItemDataLoader
{
    public static void LoadAllItems()
    {
        ResourceItems.Register();
        PlaceableItems.Register();
        ToolItems.Register();
    }
}
