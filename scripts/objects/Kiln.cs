using System;
using Godot;

public partial class Kiln : InteractableObject, IProcessingStation
{
	private CraftingRecipe _activeRecipe;
	private StationStateData _state;

	public string Label => "Kiln";
	public StationType StationType => StationType.Kiln;
	public Vector2I TileCoord => Data.TileCoord;

	private OmniLight3D _light;

	public bool IsCrafting => _state?.IsCrafting ?? false;
	public int CompletedCount => _state?.CompletedCount ?? 0;
	public int TotalCount => _state?.TotalCount ?? 0;
	public float TimeRemaining => _state?.TimeRemaining ?? 0f;
	public string ActiveRecipeId => _state?.ActiveRecipeId ?? string.Empty;


	public void BindState(StationStateData state)
	{
		_state = state;
		RefreshFromState();
	}

	private void RefreshFromState()
	{
		_activeRecipe = string.IsNullOrEmpty(_state.ActiveRecipeId)
			? null
			: CraftingRegistry.GetRecipe(_state.ActiveRecipeId);

		SetProcess(_state.IsCrafting);
		UpdateVisual();
	}

	public float GetProgress()
	{
		var displayState = GetDisplayState();
		if (displayState == null)
			return 0f;

		if (_activeRecipe == null || _activeRecipe.CraftTime <= 0f || !displayState.IsCrafting)
			return 0f;

		return (1f - displayState.TimeRemaining / _activeRecipe.CraftTime) * 100f;
	}

	public StationStateData GetDisplayState()
	{
		if (_state == null)
			return null;

		var displayState = new StationStateData
		{
			ObjectId = _state.ObjectId,
			TileCoord = _state.TileCoord,
			ActiveRecipeId = _state.ActiveRecipeId,
			TimeRemaining = _state.TimeRemaining,
			CompletedCount = _state.CompletedCount,
			TotalCount = _state.TotalCount,
			IsCrafting = _state.IsCrafting,
			LastUpdateTime = _state.LastUpdateTime
		};

		World.Sync.AdvanceStationProgress(displayState);
		return displayState;
	}

	public CraftingRecipe GetActiveRecipe()
	{
		return _activeRecipe;
	}

	public override void Interact(Player player)
	{
		player.HUD.OpenCraftingUI(this);
	}

	public override void _Ready()
	{
		base._Ready();
		_light = GetNode<OmniLight3D>("OmniLight3D");
		InteractPrompt = GetNode<InteractionPrompt>("InteractionPrompt");
		UpdateVisual();
	}

	private void UpdateVisual()
	{
		if (_light != null)
			_light.Visible = IsCrafting;
	}
}