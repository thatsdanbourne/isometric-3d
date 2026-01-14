using Godot;
using System;
using System.Collections.Generic;

public partial class CraftingUI : Control
{
	public Player Player;
	private ICraftingStation currentStation;

	private Label titleLabel;
	private VBoxContainer recipeList;
	private List<RecipeEntry> recipeEntries = new();

	private Control stationStatusRoot;
	private ItemContainerSlot resultSlot;
	private ProgressBar craftProgressBar;
	private Button collectOutputButton;

	private PackedScene recipeEntryScene =
		GD.Load<PackedScene>("res://scenes/ui/crafting/RecipeEntry.tscn");


	public override void _Ready()
	{
		GameManager.Instance.LocalPlayerChanged += (Player p) =>
		{
			Player = p;

			Player.PlayerReady += () =>
			{
				Player.Inventory.ContainerChanged += RefreshCraftingUI;
				Player.Hotbar.ContainerChanged += RefreshCraftingUI;
				RefreshCraftingUI();
			};
		};

		titleLabel = GetNode<Label>("CraftingWindow/VBoxContainer/TitleLabel");
		recipeList = GetNode<VBoxContainer>("CraftingWindow/VBoxContainer/MarginContainer/VBoxContainer/ScrollContainer/RecipeList");

		stationStatusRoot = GetNode<Control>("CraftingWindow/VBoxContainer/MarginContainer/VBoxContainer/StationStatus");
		resultSlot = stationStatusRoot.GetNode<ItemContainerSlot>("VBoxContainer/ItemContainerSlot");
		craftProgressBar = stationStatusRoot.GetNode<ProgressBar>("VBoxContainer/ProgressBar");
		collectOutputButton = stationStatusRoot.GetNode<Button>("VBoxContainer/CollectButton");

		resultSlot.IsCraftingSlot = true;
		collectOutputButton.Pressed += OnCollectPressed;
		stationStatusRoot.Visible = false;

		CallDeferred("BuildRecipeList");
	}

	public override void _Process(double delta)
	{
		UpdateStationUI();
	}

	public void OpenForStation(ICraftingStation station)
	{
		currentStation = station;
		BuildRecipeList();
	}

	public void BuildRecipeList()
	{
		recipeEntries.Clear();

		foreach (Node child in recipeList.GetChildren())
			child.QueueFree();

		var recipes = CraftingRegistry.GetRecipesByStation(currentStation?.StationType ?? StationType.None);

		foreach (var recipe in recipes)
		{
			var entry = recipeEntryScene.Instantiate<RecipeEntry>();
			entry.SetRecipe(recipe);
			entry.OnCraftRequested = CraftItem;

			recipeEntries.Add(entry);
			recipeList.AddChild(entry);
		}

		titleLabel.Text = currentStation != null ? currentStation.Label : "Hand Crafting";

		RefreshCraftingUI();
	}

	private void CraftItem(CraftingRecipe recipe)
	{
		if (CraftingManager.Instance.CanCraft(Player, recipe))
		{
			if (currentStation != null)
				currentStation.StartCraft(recipe, Player);
			else
				CraftingManager.Instance.CraftItem(Player, recipe.ResultItemId);
		}
	}

	private void OnCollectPressed()
	{
		currentStation?.CollectOutput(Player);
	}

	public void UpdateStationUI()
	{
		if (stationStatusRoot == null)
			return;

		if (currentStation == null || !currentStation.IsTimed)
		{
			stationStatusRoot.Visible = false;
			return;
		}

		stationStatusRoot.Visible = true;

		var recipe = currentStation.GetActiveRecipe();

		craftProgressBar.Value = currentStation.GetProgress() * 100f;

		if (recipe != null)
			resultSlot.SetCraftingStack(
				ItemRegistry.GetItem(recipe.ResultItemId),
				currentStation.CompletedCount,
				currentStation.TotalCount);
		else
			resultSlot.SetStack(null);
	}

	public void RefreshCraftingUI()
	{
		foreach (var entry in recipeEntries)
		{
			var recipe = entry.Recipe;
			var canCraft = CraftingManager.Instance.CanCraft(Player, recipe);

			entry.Result.Modulate = canCraft ? Colors.White : new Color(1, 1, 1, 0.5f);

			entry.Result.HoldToActivate = canCraft;

			entry.Result.AddThemeStyleboxOverride("panel",
				canCraft ? entry.slotHighlightStyle : entry.slotDefaultStyle);

			for (int i = 0; i < entry.IngredientSlots.Count; i++)
			{
				var slot = entry.IngredientSlots[i];
				var ingredientInfo = entry.Ingredients[i];

				int owned = InventoryManager.Instance.GetItemTotalCount(ingredientInfo.Item, Player.Inventory, Player.Hotbar);

				slot.CountLabel.Modulate = owned >= ingredientInfo.RequiredCount ? Colors.White : Colors.Red;
			}
		}
	}
}