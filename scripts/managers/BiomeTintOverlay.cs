using Godot;
using System.Collections.Generic;

public partial class BiomeTintOverlay : ColorRect
{
    private readonly Dictionary<string, (Color color, float strength)> BIOME_TINTS = new()
    {
        ["plains"] = (new Color(1.08f, 1.02f, 0.92f), 0.3f),
        ["forest"] = (new Color(0.9f, 1.1f, 0.90f), 0.4f),
        ["taiga"] = (new Color(0.82f, 0.92f, 1.18f), 0.55f),
        ["tundra"] = (new Color(0.88f, 0.9f, 1.20f), 0.5f),
        ["desert"] = (new Color(1.22f, 1.12f, 0.82f), 0.7f),
    };

    private string currentBiome = "";
    private Color targetTint = Colors.White;
    private float targetStrength = 0f;
    private float fadeSpeed = 1.0f;
    private bool isFading = false;


    public void SetTintForBiome(string biome)
    {
        if (biome == currentBiome) return;

        if (BIOME_TINTS.TryGetValue(biome, out var data))
        {
            targetTint = data.color;
            targetStrength = data.strength;
            isFading = true;
        }

        currentBiome = biome;
    }

    public override void _Process(double delta)
    {
        if (!isFading) return;

        float dt = (float)delta * fadeSpeed;

        var mat = (ShaderMaterial)Material;

        Color tint = (Color)mat.GetShaderParameter("biome_tint");
        Color newTint = tint.Lerp(targetTint, dt);
        mat.SetShaderParameter("biome_tint", newTint);

        float strength = (float)mat.GetShaderParameter("strength");
        float newStrength = Mathf.Lerp(strength, targetStrength, dt);
        mat.SetShaderParameter("strength", newStrength);

        if (newTint.IsEqualApprox(targetTint) && Mathf.Abs(newStrength - targetStrength) < 0.005f)
            isFading = false;
    }
}
