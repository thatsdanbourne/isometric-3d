using Godot;
using System.Collections.Generic;

public partial class DayNightCycle : Node
{
	[Export] public DirectionalLight3D Sun;
	[Export] public WorldEnvironment WorldEnvironment;

	private static readonly List<ShadowCard> _shadowCards = new();
	public static void RegisterShadow(ShadowCard card) => _shadowCards.Add(card);
	public static void UnregisterShadow(ShadowCard card) => _shadowCards.Remove(card);

	private Environment environment;

	private readonly Color MIDNIGHT_COLOR = new(0.1f, 0.1f, 0.3f);
	private readonly Color SUNRISE_COLOR = new(0.8f, 0.7f, 0.55f);
	private readonly Color MIDDAY_COLOR = new(0.9f, 0.9f, 0.85f);
	private readonly Color DUSK_COLOR = new(0.7f, 0.4f, 0.3f);

	// Constants
	private const float MIDDAY_ALTITUDE = -0.5759587f; // DegToRad(-33)
	private const float ALT_RANGE = 0.1745329f; // DegToRad(10)
	private float ISOMETRIC_Y_ROTATION = 0.1745329f; // DegToRad(-45f);

	[Export] public float DayLength = 300.0f;
	[Export] public float TimeOfDay = 0.25f;

	private float _lastStretch = 999f;

	private string lastAmbience = "";

	public override void _Ready()
	{
		environment = WorldEnvironment.Environment;
	}

	public override void _Process(double delta)
	{
		if (DayLength <= 0f) return;

		TimeOfDay = Mathf.PosMod(TimeOfDay + (float)delta / DayLength, 1.0f);

		
		UpdateAmbience();
		UpdateSun();
		UpdateEnvironment();
	}

	public override void _PhysicsProcess(double delta)
	{
		UpdateShadows();
	}

	public void UpdateSun()
	{
		float t = TimeOfDay;

		float yawAngle = Mathf.Lerp(0f, Mathf.Tau, t);
		var azimuth = yawAngle + ISOMETRIC_Y_ROTATION;

		var altitudeT = Mathf.Sin(t * Mathf.Pi);
		float altitude = Mathf.Lerp(MIDDAY_ALTITUDE - ALT_RANGE, MIDDAY_ALTITUDE, altitudeT);

		Sun.Rotation = new Vector3(altitude, azimuth, 0f);
	}

	private void UpdateEnvironment()
	{
		float t = TimeOfDay;
		float daylight = Mathf.Sin(t * Mathf.Pi);
		daylight = Mathf.Max(daylight, 0.03f);

		Color newColor;
		float u;

		if (t < 0.25f)
		{
			u = t / 0.25f;
			newColor = MIDNIGHT_COLOR.Lerp(SUNRISE_COLOR, u);
		}
		else if (t < 0.5f)
		{
			u = (t - 0.25f) / 0.25f;
			newColor = SUNRISE_COLOR.Lerp(MIDDAY_COLOR, u);
		}
		else if (t < 0.75f)
		{
			u = (t - 0.5f) / 0.25f;
			newColor = MIDDAY_COLOR.Lerp(DUSK_COLOR, u);
		}
		else
		{
			u = (t - 0.75f) / 0.25f;
			newColor = DUSK_COLOR.Lerp(MIDNIGHT_COLOR, u);
		}

		Sun.LightColor = newColor;
		Sun.LightEnergy = Mathf.Lerp(0.2f, 1.0f, daylight);

		float ambient = Mathf.Lerp(0.4f, 1.0f, daylight);
		environment.AmbientLightColor = newColor.Lerp(Colors.White, 0.01f);
		environment.AmbientLightEnergy = ambient;
	}

	private void UpdateShadows()
	{
		int count = _shadowCards.Count;
		if (count == 0) return;

		Vector3 dir = -Sun.GlobalTransform.Basis.Z.Normalized();

		var sunHeight = Mathf.Abs(dir.Y);
		float stretch = Mathf.Lerp(2f, 0.6f, sunHeight);

		dir.Y /= 0.5f;
		Basis basis = Basis.LookingAt(dir * 10f, Vector3.Up);

		for (int i = 0; i < count; i++)
			_shadowCards[i].ApplyShadow(basis, stretch);
	}

	private void UpdateAmbience()
    {
        string target = (TimeOfDay > 0.1f && TimeOfDay < 0.8f)
			?"forest_day"
			: "forest_night";

		if (target == lastAmbience) return;

		lastAmbience = target;
		AudioManager.Instance.PlayAmbience(target);
    }
}
