using Godot;

public partial class DayNightCycle : Node
{
	[Export] public float DayLength = 300.0f;
	[Export] public DirectionalLight3D Sun;
	[Export] public float TimeOfDay = 0.25f;
	[Export] public WorldEnvironment WorldEnvironment;
	[Export] public bool RunCycle = true;

	private World _world;
	public bool IsDaytime { get; private set; } = true;

	[Signal]
	public delegate void DayStateChangedEventHandler(bool isDaytime);

	// Constants
	private const float MiddayAltitude = -0.5759587f; // DegToRad(-33)
	private const float AltRange = 0.1745329f; // DegToRad(10)

	private static readonly Color MidnightColor = new(0.1f, 0.1f, 0.3f);
	private static readonly Color SunriseColor = new(0.8f, 0.7f, 0.55f);
	private static readonly Color MiddayColor = new(0.9f, 0.9f, 0.85f);
	private static readonly Color DuskColor = new(0.7f, 0.4f, 0.3f);

	private Environment _environment;
	private float _isometricYRotation = 0.1745329f; // DegToRad(-45f);

	private string _lastAmbience = "";
	private bool _lastDaytimeState = true;

	private float _sunUpdateAccumulator;
	private const float SunUpdateInterval = 1f / 10f;
	private Vector3 _lastSunRotation;
	private const float SunRotationEpsilon = 0.00005f;


	public override void _Ready()
	{
		_world = GetParent<World>();
		_environment = WorldEnvironment.Environment;
	}

	public override void _Process(double delta)
	{
		if (!RunCycle || DayLength <= 0f) return;

		_sunUpdateAccumulator += (float)delta;
		if (_sunUpdateAccumulator < SunUpdateInterval) return;
		_sunUpdateAccumulator -= SunUpdateInterval;

		var t = _world.WorldTimeSeconds / DayLength;
		TimeOfDay = (float)((t + 0.2) % 1);

		IsDaytime = TimeOfDay is > 0.1f and < 0.8f;

		if (IsDaytime != _lastDaytimeState)
		{
			EmitSignal(SignalName.DayStateChanged, IsDaytime);
			_lastDaytimeState = IsDaytime;
		}

		UpdateSun();
		UpdateEnvironment();
	}

	public void UpdateSun()
	{
		var t = TimeOfDay;

		var yawAngle = Mathf.Lerp(0f, Mathf.Tau, t);
		var azimuth = yawAngle + _isometricYRotation;

		var altitudeT = Mathf.Sin(t * Mathf.Pi);
		var altitude = Mathf.Lerp(MiddayAltitude - AltRange, MiddayAltitude, altitudeT);

		var rotation = new Vector3(altitude, azimuth, 0f);

		if (_lastSunRotation.DistanceSquaredTo(rotation) < SunRotationEpsilon * SunRotationEpsilon) return;

		_lastSunRotation = rotation;
		Sun.Rotation = rotation;
	}

	private void UpdateEnvironment()
	{
		var t = TimeOfDay;
		var daylight = Mathf.Sin(t * Mathf.Pi);
		daylight = Mathf.Max(daylight, 0.03f);

		Color newColor;
		float u;

		if (t < 0.25f)
		{
			u = t / 0.25f;
			newColor = MidnightColor.Lerp(SunriseColor, u);
		}
		else if (t < 0.5f)
		{
			u = (t - 0.25f) / 0.25f;
			newColor = SunriseColor.Lerp(MiddayColor, u);
		}
		else if (t < 0.75f)
		{
			u = (t - 0.5f) / 0.25f;
			newColor = MiddayColor.Lerp(DuskColor, u);
		}
		else
		{
			u = (t - 0.75f) / 0.25f;
			newColor = DuskColor.Lerp(MidnightColor, u);
		}

		Sun.LightColor = newColor;
		Sun.LightEnergy = Mathf.Lerp(0.2f, 1.0f, daylight);

		var ambient = Mathf.Lerp(0.1f, 0.4f, daylight);
		_environment.AmbientLightColor = newColor.Lerp(Colors.White, 0.01f);
		_environment.AmbientLightEnergy = ambient;
	}
}