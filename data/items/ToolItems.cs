using Godot;
using System.Collections.Generic;

public static class ToolItems
{
    public static void Register()
    {
        ItemRegistry.RegisterItem(new ToolItem {
            Id = "fist",
            DisplayName = "",
            Description = "",
            Damage = 1.0f,
        });

        ItemRegistry.RegisterItem(new ToolItem {
            Id = "stone_pickaxe",
            ToolType = "pickaxe",
            DisplayName = "Stone Pickaxe",
            Description = "Now go find some iron!",
            Icon = LoadTexture("stone_pickaxe"),
            StackSize = 1,
            Damage = 1.25f,
            DamageMultipliers = new Dictionary<string, float> {
                { "stone", 2.0f },
                { "wood", 0.25f }
            },
        });

        ItemRegistry.RegisterItem(new ToolItem {
            Id = "stone_axe",
            ToolType = "axe",
            DisplayName = "Stone Axe",
            Description = "Beats punching trees...",
            Icon = LoadTexture("stone_axe"),
            Damage = 1.25f,
            StackSize = 1,
            DamageMultipliers = new Dictionary<string, float> {
                { "stone", 0.25f },
                { "wood", 2.0f }
            },
        });
    }

    private static Texture2D LoadTexture(string name) =>
        GD.Load<Texture2D>($"res://assets/icons/{name}.png");
}
