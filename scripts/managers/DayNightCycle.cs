using Godot;

public partial class DayNightCycle : Node
{
    [Signal]
    public delegate void DayStateChangedEventHandler(bool isDaytime);

    // Constants
    private const float MiddayAltitude = -0.5759587f; // DegToRad(-33)
    private const float AltRange = 0.1745329f; // DegToRad(10)

    private static readonly Color MidnightColor = new(0.1f, 0.1f, 0.3f);
    private static readonly Color SunriseColor = new(0.8f, 0.7f, 0.55f);
    private static readonly Color MiddayColor = new(0.9f, 0.9f, 0.85f);
    private static readonly Color DuskColor = new(0.7f, 0.4f, 0.3f);

    private float lastStretch = 999f;

    [Export] public float DayLength = 300.0f;

    private Environment environment;
    private float isometricYRotation = 0.1745329f; // DegToRad(-45f);

    private string lastAmbience = "";
    private bool lastDaytimeState = true;

    [Export] public DirectionalLight3D Sun;
    [Export] public float TimeOfDay = 0.25f;
    [Export] public WorldEnvironment WorldEnvironment;
    public bool IsDaytime { get; private set; } = true;

    public override void _Ready()
    {
        environment = WorldEnvironment.Environment;
    }

    public override void _Process(double delta)
    {
        if (DayLength <= 0f) return;

        TimeOfDay = Mathf.PosMod(TimeOfDay + (float)delta / DayLength, 1.0f);

        IsDaytime = TimeOfDay is > 0.1f and < 0.8f;

        if (IsDaytime != lastDaytimeState)
        {
            EmitSignal(SignalName.DayStateChanged, IsDaytime);
            lastDaytimeState = IsDaytime;
        }

        UpdateSun();
        UpdateEnvironment();
    }

    public void UpdateSun()
    {
        var t = TimeOfDay;

        var yawAngle = Mathf.Lerp(0f, Mathf.Tau, t);
        var azimuth = yawAngle + isometricYRotation;

        var altitudeT = Mathf.Sin(t * Mathf.Pi);
        var altitude = Mathf.Lerp(MiddayAltitude - AltRange, MiddayAltitude, altitudeT);

        Sun.Rotation = new Vector3(altitude, azimuth, 0f);
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
        environment.AmbientLightColor = newColor.Lerp(Colors.White, 0.01f);
        environment.AmbientLightEnergy = ambient;
    }
}