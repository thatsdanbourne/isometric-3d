using Godot;
using System.Collections.Generic;

public partial class CraftingRegistry : Node
{
    public static CraftingRegistry Instance { get; private set; }

    public List<CraftingRecipe> Recipes = new();

    public override void _Ready()
    {
        Instance = this;
        LoadRecipes("res://resources/crafting/recipes");
        GD.Print($"Loaded {Recipes.Count} crafting recipes.");
    }

    private void LoadRecipes(string path)
    {
        var files = DirAccess.GetFilesAt(path);

        if (files == null || files.Length == 0)
        {
            GD.PushError("Could not open crafting recipes folder or folder is empty: " + path);
            return;
        }

        foreach (var file in files)
        {
            if (!file.EndsWith(".tres"))
                continue;

            string resPath = path + "/" + file;

            var recipe = ResourceLoader.Load<CraftingRecipe>(resPath);
            if (recipe == null)
            {
                GD.PushWarning($"Skipping invalid crafting recipe: {resPath}");
                continue;
            }

            Recipes.Add(recipe);
        }
    }
}
