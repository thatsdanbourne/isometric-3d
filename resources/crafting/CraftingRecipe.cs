using Godot;
[GlobalClass]
public partial class CraftingRecipe : Resource
{
    [Export] public string Name { get; set; }

    [ExportGroup("Ingredients")]
    [Export] public Godot.Collections.Dictionary<Item, int> Ingredients { get; set; } = new Godot.Collections.Dictionary<Item, int>();

    [ExportGroup("Result")]
    [Export] public Item ResultItem { get; set; }

    [Export] public int ResultCount { get; set; } = 1;
}
