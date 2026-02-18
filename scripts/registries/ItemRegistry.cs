using System.Collections.Generic;

public static class ItemRegistry
{
	private static readonly Dictionary<string, Item> Items = new();

	public static void RegisterItem(Item item)
	{
		Items[item.Id] = item;
	}

	public static Item GetItem(string id)
	{
		return Items.GetValueOrDefault(id);
	}

	public static IEnumerable<Item> GetAllItems()
	{
		return Items.Values;
	}
}