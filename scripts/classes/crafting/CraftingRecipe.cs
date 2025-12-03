using System.Collections.Generic;
public class CraftingRecipe
{
    public string ResultItemId;
    public int ResultCount = 1;

    public Dictionary<string, int> Ingredients { get; set; } = new();

    public string RequiredStationId = "";

    public CraftingRecipe(string resultItemId, int resultCount = 1)
    {
        ResultItemId = resultItemId;
        ResultCount = resultCount;
    }
}
