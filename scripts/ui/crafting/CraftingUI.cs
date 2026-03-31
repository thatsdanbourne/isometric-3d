using Godot;
using System.Collections.Generic;

public partial class CraftingUI : Control
{
	private Player _player;
	private bool _playerReady;

	private ICraftingStation _currentStation;

	private Label _titleLabel;
	private VBoxContainer _recipeList;
	private readonly List<RecipeEntry> _recipeEntries = new();

	private Control _stationStatusRoot;
	private ItemContainerSlot _resultSlot;
	private ProgressBar _craftProgressBar;
	private Button _collectOutputButton;

	private PackedScene _recipeEntryScene =
		GD.Load<PackedScene>("res://scenes/ui/crafting/RecipeEntry.tscn");


	public override void _Ready()
	{
		GameManager.Instance.LocalPlayerChanged += OnLocalPlayerChanged;

		_titleLabel = GetNode<Label>("CraftingWindow/VBoxContainer/TitleLabel");
		_recipeList =
			GetNode<VBoxContainer>(
				"CraftingWindow/VBoxContainer/MarginContainer/VBoxContainer/ScrollContainer/RecipeList");

		_stationStatusRoot =
			GetNode<Control>("CraftingWindow/VBoxContainer/MarginContainer/VBoxContainer/StationStatus");
		_resultSlot = _stationStatusRoot.GetNode<ItemContainerSlot>("VBoxContainer/ItemContainerSlot");
		_craftProgressBar = _stationStatusRoot.GetNode<ProgressBar>("VBoxContainer/ProgressBar");
		_collectOutputButton = _stationStatusRoot.GetNode<Button>("VBoxContainer/CollectButton");

		_resultSlot.IsCraftingSlot = true;
		_collectOutputButton.Pressed += OnCollectPressed;
		_stationStatusRoot.Visible = false;

		CallDeferred(nameof(BuildRecipeList));
	}

	private void OnLocalPlayerChanged(Player player)
	{
		if (_player != null)
		{
			_player.PlayerReady -= OnPlayerReady;

			if (_player.Inventory != null)
				_player.Inventory.ContainerChanged -= RefreshCraftingUI;

			if (_player.Hotbar != null)
				_player.Hotbar.ContainerChanged -= RefreshCraftingUI;
		}

		_player = player;
		_playerReady = false;

		if (_player == null) return;

		_player.PlayerReady += OnPlayerReady;
	}

	private void OnPlayerReady()
	{
		if (_player == null) return;

		_player.PlayerReady -= OnPlayerReady;

		_player.Inventory.ContainerChanged += RefreshCraftingUI;
		_player.Hotbar.ContainerChanged += RefreshCraftingUI;

		_playerReady = true;
		RefreshCraftingUI();
	}

	public override void _Process(double delta)
	{
		UpdateStationUI();
	}

	public void OpenForStation(ICraftingStation station)
	{
		_currentStation = station;
		BuildRecipeList();
	}

	private void BuildRecipeList()
	{
		_recipeEntries.Clear();

		foreach (var child in _recipeList.GetChildren())
			child.QueueFree();

		var recipes = CraftingRegistry.GetRecipesByStation(_currentStation?.StationType ?? StationType.None);

		foreach (var recipe in recipes)
		{
			var entry = _recipeEntryScene.Instantiate<RecipeEntry>();
			entry.SetRecipe(recipe);
			entry.OnCraftRequested = CraftItem;

			_recipeEntries.Add(entry);
			_recipeList.AddChild(entry);
		}

		_titleLabel.Text = _currentStation != null ? _currentStation.Label : "Hand Crafting";

		RefreshCraftingUI();
	}

	private void CraftItem(CraftingRecipe recipe)
	{
		if (CraftingManager.Instance.CanCraft(_player, recipe))
		{
			if (_currentStation != null)
				_currentStation.StartCraft(recipe, _player);
			else
				CraftingManager.Instance.CraftItem(_player, recipe.ResultItemId);
		}
	}

	private void OnCollectPressed()
	{
		_currentStation?.CollectOutput(_player);
	}

	private void UpdateStationUI()
	{
		if (_stationStatusRoot == null)
			return;

		if (_currentStation == null || !_currentStation.IsTimed)
		{
			_stationStatusRoot.Visible = false;
			return;
		}

		_stationStatusRoot.Visible = true;

		var recipe = _currentStation.GetActiveRecipe();

		_craftProgressBar.Value = _currentStation.GetProgress() * 100f;

		if (recipe != null)
			_resultSlot.SetCraftingStack(
				ItemRegistry.GetItem(recipe.ResultItemId),
				_currentStation.CompletedCount,
				_currentStation.TotalCount);
		else
			_resultSlot.SetStack(null);
	}

	private void RefreshCraftingUI()
	{
		if (!_playerReady) return;

		foreach (var entry in _recipeEntries)
		{
			var recipe = entry.Recipe;
			var canCraft = CraftingManager.Instance.CanCraft(_player, recipe);

			entry.Result.Modulate = canCraft ? Colors.White : new Color(1, 1, 1, 0.5f);

			entry.Result.HoldToActivate = canCraft;

			entry.Result.Highlight.Visible = canCraft;

			for (var i = 0; i < entry.IngredientSlots.Count; i++)
			{
				var slot = entry.IngredientSlots[i];
				var ingredientInfo = entry.Ingredients[i];

				var owned = InventoryManager.Instance.GetItemTotalCount(ingredientInfo.Item, _player.Inventory,
					_player.Hotbar);

				slot.CountLabel.Modulate = owned >= ingredientInfo.RequiredCount ? Colors.White : Colors.Red;
			}
		}
	}
}