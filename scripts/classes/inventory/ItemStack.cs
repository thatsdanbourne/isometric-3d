using Godot;

public class ItemStack(Item item, int count)
{
	public Item Item { get; private set; } = item;
	public int Count { get; set; } = Mathf.Clamp(count, 1, item.StackSize);

	private int StackSize => Item.StackSize;

	public bool IsFull => Count >= StackSize;

	public int Add(int amount)
	{
		var space = StackSize - Count;
		var added = Mathf.Min(space, amount);
		Count += added;
		return amount - added;
	}

	public int Remove(int amount)
	{
		var removed = Mathf.Min(Count, amount);
		Count -= removed;
		return removed;
	}
}