using Godot;

[GlobalClass]
public partial class CraftingRecipe : Resource
{
    [Export] public string Name;
    [Export] public Godot.Collections.Dictionary<Item, int> Ingredients;
}
