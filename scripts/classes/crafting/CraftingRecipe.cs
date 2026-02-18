using System.Collections.Generic;

public class CraftingRecipe(string resultItemId, int resultCount = 1)
{
	public readonly string ResultItemId = resultItemId;
	public readonly int ResultCount = resultCount;
	public Dictionary<string, int> Ingredients { get; set; } = new();
	public StationType RequiredStation = StationType.None;
	public float CraftTime = 0f;
}