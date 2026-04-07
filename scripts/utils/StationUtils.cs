using Godot;
using System;

public static class StationUtils
{
	public static ItemStack CloneStack(ItemStack stack)
	{
		return stack?.Clone();
	}

	public static ItemStack[] CloneSlots(ItemStack[] slots)
	{
		if (slots == null)
			return [];

		var result = new ItemStack[slots.Length];
		for (var i = 0; i < slots.Length; i++)
			result[i] = CloneStack(slots[i]);

		return result;
	}
}