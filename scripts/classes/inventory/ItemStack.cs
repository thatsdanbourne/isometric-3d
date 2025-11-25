using Godot;
using System;

public partial class ItemStack : GodotObject
{
    public Item Item { get; private set; }
    public int Count { get; set; }
    
    public int StackSize => Item.StackSize;

    public ItemStack(Item item, int count)
    {
        Item = item;
        Count = Mathf.Clamp(count, 1, item.StackSize);
    }

    public bool IsFull => Count >= StackSize;

    public int Add(int amount)
    {
        int space = StackSize - Count;
        int added = Mathf.Min(space, amount);
        Count += added;
        return amount - added;
    }

    public int Remove(int amount)
    {
        int removed = Mathf.Min(Count, amount);
        Count -= removed;
        return removed;
    }
}
