using Godot;
using System;
using System.Collections.Generic;

public partial class RecipeEntry : HBoxContainer
{
	[Export] public HBoxContainer IngredientsContainer { get; set; }
	[Export] public ItemContainerSlot Result { get; set; }

	public readonly List<ItemContainerSlot> IngredientSlots = new();
	public readonly List<IngredientInfo> Ingredients = new();
	public CraftingRecipe Recipe { get; private set; }

	public Action<CraftingRecipe> OnCraftRequested;

	private PackedScene _containerSlotScene = GD.Load<PackedScene>("res://scenes/ui/HUD/ItemContainerSlot.tscn");

	public StyleBoxFlat SlotDefaultStyle =
		ResourceLoader.Load<StyleBoxFlat>("res://resources/ui/ItemContainerSlotStyle.tres");

	public StyleBoxFlat SlotHighlightStyle =
		ResourceLoader.Load<StyleBoxFlat>("res://resources/ui/ItemContainerSlotHighlight.tres");


	public void SetRecipe(CraftingRecipe r)
	{
		Ingredients.Clear();
		IngredientSlots.Clear();

		foreach (var ingredient in r.Ingredients)
		{
			var item = ItemRegistry.GetItem(ingredient.Key);
			var requiredCount = ingredient.Value;
			Ingredients.Add(new IngredientInfo(item, requiredCount));

			var ingredientSlot = _containerSlotScene.Instantiate<ItemContainerSlot>();
			ingredientSlot.SetStack(new ItemStack(item, requiredCount));
			ingredientSlot.ReadOnly = true;

			var ingIcon = ingredientSlot.GetNode<TextureRect>("Icon");
			var ingCount = ingredientSlot.GetNode<Label>("Label");
			ingIcon.Texture = item.Icon;
			ingCount.Text = requiredCount.ToString();

			IngredientSlots.Add(ingredientSlot);
			IngredientsContainer.AddChild(ingredientSlot);
		}

		Recipe = r;
		var recipeResultItem = ItemRegistry.GetItem(r.ResultItemId);

		var resIcon = Result.GetNode<TextureRect>("Icon");
		var resCount = Result.GetNode<Label>("Label");
		resIcon.Texture = recipeResultItem.Icon;
		resCount.Text = r.ResultCount.ToString();

		Result.SetStack(new ItemStack(recipeResultItem, r.ResultCount));
		Result.HoldToActivate = true;
		Result.SlotHoldCompleted += OnCraftHoldCompleted;
		Result.AddThemeStyleboxOverride("panel", SlotHighlightStyle);
	}

	private void OnCraftHoldCompleted()
	{
		OnCraftRequested?.Invoke(Recipe);
	}
}


public struct IngredientInfo(Item item, int count)
{
	public readonly Item Item = item;
	public readonly int RequiredCount = count;
}