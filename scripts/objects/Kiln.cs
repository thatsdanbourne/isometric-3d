using System;
using Godot;

public partial class Kiln : WorldObject, IInteractable, ICraftingStation, IChunkStateful<StationStateData>
{
    public string Label => "Kiln";
    public StationType StationType => StationType.Kiln;

    private CraftingRecipe activeRecipe;

    private InteractionPrompt interactPrompt;

    private OmniLight3D light;
    private float timeRemaining;

    public bool IsCrafting { get; private set; }

    public bool IsTimed => true;
    public int CompletedCount { get; private set; }

    public int TotalCount { get; private set; }

    public float GetProgress()
    {
        if (activeRecipe == null) return 0f;
        return 1f - timeRemaining / activeRecipe.CraftTime;
    }

    public CraftingRecipe GetActiveRecipe()
    {
        return activeRecipe;
    }

    public void StartCraft(CraftingRecipe recipe, Player player)
    {
        if (activeRecipe == null)
        {
            activeRecipe = recipe;
            TotalCount = 0;
            CompletedCount = 0;
            timeRemaining = recipe.CraftTime;
            IsCrafting = true;
            SetProcess(true);
        }

        if (activeRecipe != recipe)
            return;

        // Consume ingredients per item added
        CraftingManager.Instance.ConsumeIngredients(player, recipe);
        TotalCount += 1;

        light.Visible = true;
    }

    public void CollectOutput(Player player)
    {
        if (CompletedCount <= 0 || activeRecipe == null) return;

        InventoryManager.Instance.AddItem(player, ItemRegistry.GetItem(activeRecipe.ResultItemId), CompletedCount);
        TotalCount -= CompletedCount;
        CompletedCount = 0;

        if (TotalCount <= 0)
            activeRecipe = null;
    }

    public void OnFocusGained()
    {
        interactPrompt.ShowIcon();
    }

    public void OnFocusLost()
    {
        interactPrompt.HideIcon();
    }

    public T GetCapability<T>() where T : class
    {
        return this as T;
    }

    public override void _Ready()
    {
        base._Ready();
        light = GetNode<OmniLight3D>("OmniLight3D");
        interactPrompt = GetNode<InteractionPrompt>("InteractionPrompt");
    }

    private void EndCraft()
    {
        IsCrafting = false;
        timeRemaining = 0f;
        SetProcess(false);
        light.Visible = false;
    }

    public override void _Process(double delta)
    {
        if (!IsCrafting || activeRecipe == null) return;

        timeRemaining -= (float)delta;

        while (timeRemaining <= 0f && CompletedCount < TotalCount)
        {
            CompletedCount++;
            timeRemaining += activeRecipe.CraftTime;
        }

        if (CompletedCount >= TotalCount)
            EndCraft();
    }

    private void AdvanceProgress(double elapsed)
    {
        if (activeRecipe == null || !IsCrafting) return;

        double duration = activeRecipe.CraftTime;
        var itemsCompleted = (float)(elapsed / duration);
        if (itemsCompleted <= 0f) return;

        var wholeItems = (int)Math.Floor(itemsCompleted);
        var fractional = itemsCompleted - wholeItems;
        CompletedCount += wholeItems;
        timeRemaining -= fractional * (float)duration;
        timeRemaining = Math.Max(0f, timeRemaining);

        if (CompletedCount < TotalCount) return;
        CompletedCount = TotalCount;
        timeRemaining = 0f;
        IsCrafting = false;
        SetProcess(false);
    }

    // Capture/restore state for chunk saving/loading
    public StationStateData CaptureState()
    {
        return new StationStateData
        {
            ObjectId = Data.Definition.Id,
            TileCoord = Data.TileCoord,
            ActiveRecipeId = activeRecipe?.ResultItemId,
            TimeRemaining = timeRemaining,
            CompletedCount = CompletedCount,
            TotalCount = TotalCount,
            IsCrafting = IsCrafting,
            LastUpdateTime = World.WorldTimeSeconds
        };
    }

    public void RestoreState(StationStateData stateData)
    {
        if (string.IsNullOrEmpty(stateData.ActiveRecipeId)) return;

        activeRecipe = CraftingRegistry.GetRecipeByResultId(stateData.ActiveRecipeId);
        timeRemaining = stateData.TimeRemaining;
        CompletedCount = stateData.CompletedCount;
        TotalCount = stateData.TotalCount;
        IsCrafting = stateData.IsCrafting;

        var elapsed = World.WorldTimeSeconds - stateData.LastUpdateTime;
        AdvanceProgress(elapsed);

        SetProcess(IsCrafting);
        light.Visible = IsCrafting;
    }
}