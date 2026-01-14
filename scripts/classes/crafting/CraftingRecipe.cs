using System.Collections.Generic;
public class CraftingRecipe
{
    public string ResultItemId;
    public int ResultCount = 1;
    public Dictionary<string, int> Ingredients { get; set; } = new();
    public StationType RequiredStation = StationType.None;
    public float CraftTime = 0f;

    public CraftingRecipe(string resultItemId, int resultCount = 1)
    {
        ResultItemId = resultItemId;
        ResultCount = resultCount;
    }
}
