using Godot;
using System.Collections.Generic;

public partial class CraftingUI : Control
{
	public Player Player;
	public Inventory Inventory;
	public Hotbar Hotbar;

	private VBoxContainer recipeList;
	private TextureRect resultIcon;
	private Label resultName;
	private Button craftButton;

	private CraftingRecipe selectedRecipe;

	public override void _Ready()
	{
		Player = GetNode<Player>("/root/Game/World/WorldObjects/Player");
		Inventory = Player.GetNode<Inventory>("Inventory");
		Hotbar = Player.GetNode<Hotbar>("Hotbar");

		recipeList = GetNode<VBoxContainer>("CraftingWindow/VBoxContainer/ScrollContainer/RecipeList");
		resultIcon = GetNode<TextureRect>("CraftingWindow/VBoxContainer/MarginContainer/HBoxContainer/ResultIcon");
		resultName = GetNode<Label>("CraftingWindow/VBoxContainer/MarginContainer/HBoxContainer/ResultName");
		craftButton = GetNode<Button>("CraftingWindow/VBoxContainer/MarginContainer/HBoxContainer/CraftButton");

		craftButton.Pressed += OnCraftButtonPressed;

		PopulateRecipeList();
		ClearSelected();
	}

	private void PopulateRecipeList()
	{
		foreach (var recipe in CraftingRegistry.Instance.Recipes)
        {
            var btn = new Button()
            {
                Text = recipe.Name,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				Alignment = HorizontalAlignment.Left
            };

			btn.Pressed += () => SelectRecipe(recipe);

			recipeList.AddChild(btn);
        }
	}

	private void SelectRecipe(CraftingRecipe recipe)
    {
		selectedRecipe = recipe;

		resultIcon.Texture = recipe.ResultItem.Icon;
		resultName.Text = $"{recipe.ResultItem.DisplayName} x{recipe.ResultCount}";

		craftButton.Disabled = !CraftingManager.Instance.CanCraft(Inventory, Hotbar, recipe);
    }

	private void OnCraftButtonPressed()
    {
        if (selectedRecipe == null) return;

		CraftingManager.Instance.CraftItem(Inventory, Hotbar, selectedRecipe);
	}

	private void ClearSelected()
	{
		selectedRecipe = null;
		resultIcon.Texture = null;
		resultName.Text = "";
		craftButton.Disabled = true;
	}
}