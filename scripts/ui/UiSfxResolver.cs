using Godot;
using System;

public static class UiSfxResolver
{
	public static string GetInventorySfx(ItemStack stack, InventorySfxAction action)
	{
		var set = stack?.Item?.SoundSet ?? "generic";
		var actionStr = action.ToString().ToLowerInvariant();
		return $"ui_inventory_{set}_{actionStr}";
	}

	public static string GetCraftingSfx(CraftingRecipe recipe)
	{
		var result = ItemRegistry.GetItem(recipe.ResultItemId);

		if (result.SoundSet == "ingot")
			return "ui_kiln_place";

		return "ui_craft_generic";
	}
}