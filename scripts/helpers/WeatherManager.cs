using Godot;

public partial class WeatherManager : Node
{
    public enum WeatherType { Clear, Rain, Snow }

    [Export] public Node3D ParticleAnchor;
    [Export] public GpuParticles2D RainParticles;
    [Export] public GpuParticles2D SnowParticles;
    [Export] public Player Player;

    public WeatherType CurrentWeather = WeatherType.Clear;
    public float Intensity = 0f;

    private WeatherType targetWeather;
    private float targetIntensity = 0f;
    private float weatherTransitionSpeed = 0.3f;
    private float fadeLerpSpeed = 0.0005f;

    private float weatherTimer = 0f;
    private float nextWeatherChange = 20f;

    private float RainFade = 0f;
    private float SnowFade = 0f;

    private RandomNumberGenerator rng = new();


    public override void _Ready()
    {
        targetWeather = CurrentWeather;
        rng.Randomize();

        UpdateOverlay();
        GetViewport().Connect("size_changed", new Callable(this, nameof(UpdateOverlay)));
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        weatherTimer += dt;
        if (weatherTimer >= nextWeatherChange)
        {
            weatherTimer = 0f;
            nextWeatherChange = rng.RandfRange(60f, 120f);
            ChooseWeatherForBiome(Player.CurrentBiome);
        }

        Intensity = Mathf.Lerp(Intensity, targetIntensity, dt * weatherTransitionSpeed);

        UpdateWeather();
    }

    private void UpdateWeather()
    {
        float rainTarget =
            (targetWeather == WeatherType.Rain) ? targetIntensity : 0f;

        float snowTarget =
            (targetWeather == WeatherType.Snow) ? targetIntensity : 0f;


        RainFade = Mathf.Lerp(RainFade, rainTarget, fadeLerpSpeed);
        SnowFade = Mathf.Lerp(SnowFade, snowTarget, fadeLerpSpeed);


        bool rainActive = RainFade > 0.01f;
        RainParticles.Visible = rainActive;
        RainParticles.Emitting = rainActive;

        RainParticles.Modulate = new Color(1, 1, 1, RainFade);
        RainParticles.AmountRatio = RainFade;


        bool snowActive = SnowFade > 0.01f;
        SnowParticles.Visible = snowActive;
        SnowParticles.Emitting = snowActive;

        SnowParticles.Modulate = new Color(1, 1, 1, SnowFade);
        SnowParticles.AmountRatio = SnowFade;


        if (RainFade < 0.02f && SnowFade < 0.02f)
            CurrentWeather = WeatherType.Clear;
        else if (RainFade > SnowFade)
            CurrentWeather = WeatherType.Rain;
        else
            CurrentWeather = WeatherType.Snow;
    }

    public void ChooseWeatherForBiome(string biome)
    {
        if (!BiomeWeather.Rules.TryGetValue(biome, out var rules))
            return;

        float roll = rng.Randf();
        float acc = 0f;

        foreach (var (type, chance) in rules)
        {
            acc += chance;
            if (roll <= acc)
            {
                float newIntensity = rng.RandfRange(0.4f, 1f);
                SetWeather(type, newIntensity);
                GD.Print($"Weather changed to {type} with intensity {newIntensity} in biome {biome}");
                return;
            }
        }
    }

    public void SetWeather(WeatherType type, float intensity = 1f)
    {
        targetWeather = type;
        targetIntensity = intensity;
    }

    private void UpdateOverlay()
    {
        var viewport = GetViewport();
        Vector2 size = viewport.GetVisibleRect().Size;

        var mat = (ParticleProcessMaterial)RainParticles.ProcessMaterial;

        if (mat != null)
            mat.EmissionBoxExtents = new Vector3(size.X / 2, 0, 0);

        RainParticles.Position = new Vector2(size.X / 2, 0);


        var snowMat = (ParticleProcessMaterial)SnowParticles.ProcessMaterial;

        if (snowMat != null)
            snowMat.EmissionBoxExtents = new Vector3(size.X / 2, 0, 0);

        SnowParticles.Position = new Vector2(size.X / 2, 0);
    }
}
