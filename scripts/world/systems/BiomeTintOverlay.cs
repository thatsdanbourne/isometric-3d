using Godot;
using System.Collections.Generic;

public partial class BiomeTintOverlay : ColorRect
{
	private readonly Dictionary<BiomeId, (Color color, float strength)> _biomeTints = new()
	{
		[BiomeId.Plains] = (new Color(1.08f, 1.02f, 0.92f), 0.3f),
		[BiomeId.Forest] = (new Color(0.9f, 1.1f, 0.90f), 0.4f),
		[BiomeId.Taiga] = (new Color(0.82f, 0.92f, 1.18f), 0.55f),
		[BiomeId.Tundra] = (new Color(0.88f, 0.9f, 1.20f), 0.5f),
		[BiomeId.Desert] = (new Color(1.22f, 1.12f, 0.82f), 0.7f)
	};

	private BiomeId _currentBiome = BiomeId.Unknown;
	private Color _targetTint = Colors.White;
	private float _targetStrength;
	private float _fadeSpeed = 1.0f;
	private bool _isFading;


	public void SetTintForBiome(BiomeId biome)
	{
		if (biome == _currentBiome) return;

		if (_biomeTints.TryGetValue(biome, out var data))
		{
			_targetTint = data.color;
			_targetStrength = data.strength;
			_isFading = true;
		}

		_currentBiome = biome;
	}

	public override void _Process(double delta)
	{
		if (!_isFading) return;

		var dt = (float)delta * _fadeSpeed;

		var mat = (ShaderMaterial)Material;

		var tint = (Color)mat.GetShaderParameter("biome_tint");
		var newTint = tint.Lerp(_targetTint, dt);
		mat.SetShaderParameter("biome_tint", newTint);

		var strength = (float)mat.GetShaderParameter("strength");
		var newStrength = Mathf.Lerp(strength, _targetStrength, dt);
		mat.SetShaderParameter("strength", newStrength);

		if (newTint.IsEqualApprox(_targetTint) && Mathf.Abs(newStrength - _targetStrength) < 0.005f)
			_isFading = false;
	}
}