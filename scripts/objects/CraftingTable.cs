public partial class CraftingTable : WorldObject, IInteractable, ICraftingStation
{
    private InteractionPrompt interactPrompt;

    public string Label => "Crafting Table";
    public StationType StationType => StationType.CraftingTable;

    public bool IsCrafting => false;
    public bool IsTimed => false;

    public int CompletedCount => 0;
    public int TotalCount => 0;

    public CraftingRecipe GetActiveRecipe()
    {
        return null;
    }

    public float GetProgress()
    {
        return 0f;
    }

    public void StartCraft(CraftingRecipe recipe, Player player)
    {
        if (!CraftingManager.Instance.CanCraft(player, recipe))
            return;

        CraftingManager.Instance.CraftItem(player, recipe.ResultItemId);
    }

    public void CollectOutput(Player player)
    {
        // No output to collect in a crafting table
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
        interactPrompt = GetNode<InteractionPrompt>("InteractionPrompt");
    }
}