using Godot;
using System;
using System.Collections.Generic;

public partial class RecipeEntry : HBoxContainer
{
	[Signal] public delegate void CraftItemEventHandler(CraftingRecipe recipe);

	[Export] public HBoxContainer IngredientsContainer { get; set; }
	[Export] public ItemContainerSlot Result { get; set; }

	public List<ItemContainerSlot> IngredientSlots = new();
	public List<IngredientInfo> Ingredients = new();
	public CraftingRecipe Recipe;

	public PackedScene containerSlotScene = GD.Load<PackedScene>("res://scenes/ui/HUD/ItemContainerSlot.tscn");
	public StyleBoxFlat slotDefaultStyle = ResourceLoader.Load<StyleBoxFlat>("res://resources/ui/ItemContainerSlotStyle.tres");
	public StyleBoxFlat slotHighlightStyle = ResourceLoader.Load<StyleBoxFlat>("res://resources/ui/ItemContainerSlotHighlight.tres");
	

	public void SetRecipe(CraftingRecipe r, Player player)
    {
		Ingredients.Clear();
		IngredientSlots.Clear();

		foreach (var ingredient in r.Ingredients)
        {
			Item item = ingredient.Key;
			int requiredCount = ingredient.Value;
			Ingredients.Add(new IngredientInfo(item, requiredCount));
			
			var ingredientSlot = containerSlotScene.Instantiate<ItemContainerSlot>();
			ingredientSlot.SetStack(new ItemStack(item, requiredCount));
			ingredientSlot.ReadOnly = true;
			
			var ingIcon = ingredientSlot.GetNode<TextureRect>("Icon");
			var ingCount = ingredientSlot.GetNode<Label>("Label");
			ingIcon.Texture = ingredient.Key.Icon;
			ingCount.Text = ingredient.Value.ToString();

			IngredientSlots.Add(ingredientSlot);
			IngredientsContainer.AddChild(ingredientSlot);
        }

		Recipe = r;

		var resIcon = Result.GetNode<TextureRect>("Icon");
		var resCount = Result.GetNode<Label>("Label");
		resIcon.Texture = r.ResultItem.Icon;
		resCount.Text = r.ResultCount.ToString();

		Result.SetStack(new ItemStack(r.ResultItem, r.ResultCount));
		Result.HoldToActivate = true;
		Result.SlotHoldCompleted += OnCraftHoldCompleted;
		Result.AddThemeStyleboxOverride("panel", slotHighlightStyle);
    }

	private void OnCraftHoldCompleted()
	{
		EmitSignal(SignalName.CraftItem, Recipe);
	}
}


public struct IngredientInfo
{
	public Item Item;
	public int RequiredCount;

	public IngredientInfo(Item item, int count)
	{
		Item = item;
		RequiredCount = count;
	}
}
