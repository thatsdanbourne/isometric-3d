using Godot;
using System.Collections.Generic;

public partial class ItemRegistry
{
    private static Dictionary<string, Item> _items = new();

    public static void RegisterItem(Item item)
    {
        _items[item.Id] = item;
    }

    public static Item GetItem(string id)
    {
        return _items.TryGetValue(id, out var item) ? item : null;
    }

    public static IEnumerable<Item> GetAllItems()
    {
        return _items.Values;
    }
}
