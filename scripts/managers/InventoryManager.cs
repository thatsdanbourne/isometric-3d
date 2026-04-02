using Godot;
using System;

public partial class InventoryManager : Node
{
	public static InventoryManager Instance { get; private set; }

	public override void _Ready()
	{
		Instance = this;
	}

	// add logic
	public void AddItem(Player player, Item item, int amount)
	{
		var inv = player.Inventory;
		var hotbar = player.Hotbar;

		var remaining = amount;

		// Try to merge into existing stacks in hotbar first
		remaining = MergeStacks(hotbar, item, remaining);

		// Then try to merge into existing stacks in inventory
		remaining = MergeStacks(inv, item, remaining);

		// Then try to add into empty slots in hotbar
		remaining = FillEmptySlots(hotbar, item, remaining);

		// Finally try to add into empty slots in inventory
		remaining = FillEmptySlots(inv, item, remaining);

		if (remaining > 0)
			DropItem(player, item, remaining);

		player.HUD.RefreshUI();
	}


	// remove logic
	public int RemoveItem(Player player, Item item, int amount)
	{
		var remaining = RemoveFromContainer(player.Inventory, item, amount);

		if (remaining > 0)
			remaining = RemoveFromContainer(player.Hotbar, item, remaining);

		return remaining;
	}

	private int RemoveFromContainer(IItemContainer container, Item item, int remaining)
	{
		for (var i = 0; i < container.SlotCount && remaining > 0; i++)
		{
			var stack = container.GetSlot(i);
			if (stack == null || stack.Item != item) continue;

			var remove = Math.Min(stack.Count, remaining);
			stack.Count -= remove;
			remaining -= remove;

			container.SetSlot(i, stack.Count <= 0 ? null : stack);
		}

		return remaining;
	}


	// merge logic
	private int MergeStacks(IItemContainer container, Item item, int remaining)
	{
		for (var i = 0; i < container.SlotCount && remaining > 0; i++)
		{
			var slot = container.GetSlot(i);
			if (slot == null || slot.Item != item) continue;

			var space = slot.Item.StackSize - slot.Count;
			if (space <= 0) continue;

			var toAdd = Mathf.Min(space, remaining);
			slot.Count += toAdd;
			remaining -= toAdd;

			container.SetSlot(i, slot);
		}

		return remaining;
	}


	// fill empty slots
	private int FillEmptySlots(IItemContainer container, Item item, int remaining)
	{
		for (var i = 0; i < container.SlotCount && remaining > 0; i++)
		{
			if (container.GetSlot(i) != null)
				continue;

			var toAdd = Mathf.Min(item.StackSize, remaining);
			container.SetSlot(i, new ItemStack(item, toAdd));
			remaining -= toAdd;
		}

		return remaining;
	}


	// drop logic
	public void DropItem(Player player, Item item, int remaining)
	{
		var itemScene = ResourceLoader.Load<PackedScene>("res://scenes/ItemPickup.tscn");

		while (remaining > 0)
		{
			var amount = Mathf.Min(item.StackSize, remaining);
			remaining -= amount;

			var drop = itemScene.Instantiate<ItemPickup>();
			drop.Item = item;
			drop.Count = amount;

			GameManager.Instance.CurrentWorld.ItemPickupContainer.AddChild(drop);
			drop.GlobalPosition = player.GlobalPosition + new Vector3(0, 1, 0);
		}
	}

	// swap logic
	public void SwapStacks(IItemContainer src, int si, IItemContainer dest, int di)
	{
		var srcStack = src.GetSlot(si);
		var destStack = dest.GetSlot(di);

		src.SetSlot(si, destStack);
		dest.SetSlot(di, srcStack);
	}


	// inventory shortcut logic

	public ItemStack LeftClick(IItemContainer container, int index, ItemStack dragged)
	{
		var stack = container.GetSlot(index);

		// pick up stack
		if (dragged == null && stack != null)
		{
			container.SetSlot(index, null);
			return stack;
		}

		// place stack
		if (dragged != null && stack == null)
		{
			container.SetSlot(index, dragged);
			return null;
		}

		// merge stacks
		if (dragged != null && dragged.Item == stack.Item)
		{
			var max = stack.Item.StackSize;
			var space = max - stack.Count;

			if (space > 0)
			{
				var move = Math.Min(space, dragged.Count);
				stack.Count += move;
				dragged.Count -= move;

				return dragged.Count <= 0 ? null : dragged;
			}
		}

		// swap stacks
		if (dragged != null && dragged.Item != stack.Item)
		{
			container.SetSlot(index, dragged);
			return stack;
		}

		return dragged;
	}


	public ItemStack RightClick(IItemContainer container, int index, ItemStack dragged)
	{
		var stack = container.GetSlot(index);

		// place one item from dragged stack
		if (dragged != null)
		{
			if (stack == null)
			{
				container.SetSlot(index, new ItemStack(dragged.Item, 1));
				dragged.Count -= 1;
				return dragged.Count > 0 ? dragged : null;
			}

			if (stack.Item == dragged.Item && stack.Count < stack.Item.StackSize)
			{
				stack.Count += 1;
				dragged.Count -= 1;
				return dragged.Count > 0 ? dragged : null;
			}

			return dragged;
		}

		// pick up half of the stack
		if (stack == null) return null;

		var half = stack.Count / 2;
		if (half <= 0)
			return null;

		stack.Count -= half;
		return new ItemStack(stack.Item, half);
	}

	public ItemStack ShiftClick(IItemContainer source, int index, params IItemContainer[] targets)
	{
		var stack = source.GetSlot(index);
		if (stack == null)
			return null;


		var remaining = stack.Count;


		foreach (var target in targets)
		{
			remaining = MergeStacks(target, stack.Item, remaining);
			if (remaining <= 0) break;


			remaining = FillEmptySlots(target, stack.Item, remaining);
			if (remaining <= 0) break;
		}


		if (remaining <= 0)
		{
			source.SetSlot(index, null);
			return null;
		}


		// partial move
		source.SetSlot(index, new ItemStack(stack.Item, remaining));
		return new ItemStack(stack.Item, remaining);
	}

	private void Set(bool isHotbar, int index, ItemStack stack, Inventory inventory, Hotbar hotbar)
	{
		if (isHotbar)
			hotbar.SetSlot(index, stack);
		else
			inventory.SetSlot(index, stack);
	}

	// helpers
	public int GetItemTotalCount(Item item, Inventory inventory, Hotbar hotbar)
	{
		return inventory.GetItemCount(item) + hotbar.GetItemCount(item);
	}
}