using Godot;


public partial class Campfire : WorldObject
{
	private OmniLight3D light;
	private float baseEnergy = 2f;
	private float flickerTimer = 0f;
	private float targetEnergy = 2f;
	private float flickerMin = -0.5f;
	private float flickerMax = 0.5f;
	private float flickerLerpSpeed = 6f;

	public override void _Ready()
	{
		CurrentHealth = MaxHealth;
		light = GetNode<OmniLight3D>("OmniLight3D");
		light.LightEnergy = baseEnergy;
		targetEnergy = baseEnergy;

		base._Ready();
	}

	public override void _Process(double delta)
	{
		flickerTimer -= (float)delta;

		if (flickerTimer <= 0f)
		{
			targetEnergy = baseEnergy + (float)GD.RandRange(flickerMin, flickerMax);
			flickerTimer = (float)GD.RandRange(0.05f, 0.12f);
		}

		light.LightEnergy = Mathf.Lerp(light.LightEnergy, targetEnergy, flickerLerpSpeed * (float)delta);

		base._Process(delta);
	}
}