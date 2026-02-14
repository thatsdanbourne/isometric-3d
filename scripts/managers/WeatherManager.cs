using Godot;

public partial class WeatherManager : Node
{
    [Export] public Node3D ParticleAnchor;
    [Export] public GpuParticles2D RainParticles;
    [Export] public GpuParticles2D SnowParticles;
    [Export] public WorldEnvironment WorldEnvironment;

    private WeatherType currentWeather = WeatherType.Rain;
    private float currentIntensity = 0.5f;

    private WeatherType targetWeather;
    private float targetIntensity;
    private float weatherTransitionSpeed = 0.3f;
    private float fadeLerpSpeed = 0.0005f;

    private float weatherTimer;
    private float nextWeatherChange = 20f;

    private float rainFade;
    private float snowFade;

    private float rainSunDim = 0.75f;
    private float snowSunDim = 0.85f;

    private float baseFog;
    private float rainFog = 0.3f;
    private float snowFog = 0.5f;

    private float SunlightMultiplier { get; set; } = 1f;

    private float currentFog;

    private string currentBiome = "";

    private RandomNumberGenerator rng = new();


    public override void _Ready()
    {
        targetWeather = currentWeather;
        rng.Randomize();

        UpdateOverlay();
        GetViewport().Connect("size_changed", new Callable(this, nameof(UpdateOverlay)));
    }


    public override void _Process(double delta)
    {
        var dt = (float)delta;

        weatherTimer += dt;
        if (weatherTimer >= nextWeatherChange)
        {
            weatherTimer = 0f;
            nextWeatherChange = rng.RandfRange(60f, 180f);

            if (!string.IsNullOrEmpty(currentBiome))
                ChooseWeatherForBiome(currentBiome);
        }

        currentIntensity = Mathf.Lerp(currentIntensity, targetIntensity, dt * weatherTransitionSpeed);

        UpdateWeather();
        UpdateLightingAndFog(dt);
    }

    public void SetBiome(string biome)
    {
        if (biome == currentBiome) return;

        currentBiome = biome;
        weatherTimer = 0f;
        nextWeatherChange = rng.RandfRange(10f, 30f);

        ChooseWeatherForBiome(biome);
    }

    private void UpdateWeather()
    {
        var rainTarget =
            targetWeather == WeatherType.Rain ? targetIntensity : 0f;

        var snowTarget =
            targetWeather == WeatherType.Snow ? targetIntensity : 0f;


        rainFade = Mathf.Lerp(rainFade, rainTarget, fadeLerpSpeed);
        snowFade = Mathf.Lerp(snowFade, snowTarget, fadeLerpSpeed);


        var rainActive = rainFade > 0.01f;
        RainParticles.Visible = rainActive;
        RainParticles.Emitting = rainActive;

        RainParticles.Modulate = new Color(1, 1, 1, rainFade);
        RainParticles.AmountRatio = rainFade;


        var snowActive = snowFade > 0.01f;
        SnowParticles.Visible = snowActive;
        SnowParticles.Emitting = snowActive;

        SnowParticles.Modulate = new Color(1, 1, 1, snowFade);
        SnowParticles.AmountRatio = snowFade;


        if (rainFade < 0.02f && snowFade < 0.02f)
            currentWeather = WeatherType.Clear;
        else if (rainFade > snowFade)
            currentWeather = WeatherType.Rain;
        else
            currentWeather = WeatherType.Snow;
    }

    private void UpdateLightingAndFog(float dt)
    {
        float targetMultiplier;

        switch (targetWeather)
        {
            case WeatherType.Rain:
                targetMultiplier = Mathf.Lerp(1f, rainSunDim, targetIntensity);
                break;

            case WeatherType.Snow:
                targetMultiplier = Mathf.Lerp(1f, snowSunDim, targetIntensity);
                break;

            case WeatherType.Clear:
            default:
                targetMultiplier = 1f;
                break;
        }

        SunlightMultiplier = Mathf.Lerp(SunlightMultiplier, targetMultiplier, dt * 0.5f);

        if (WorldEnvironment == null) return;

        float targetFog;

        switch (targetWeather)
        {
            case WeatherType.Rain:
                targetFog = Mathf.Lerp(baseFog, rainFog, targetIntensity);
                break;

            case WeatherType.Snow:
                targetFog = Mathf.Lerp(baseFog, snowFog, targetIntensity);
                break;

            case WeatherType.Clear:
            default:
                targetFog = baseFog;
                break;
        }

        currentFog = Mathf.Lerp(currentFog, targetFog, fadeLerpSpeed * 0.75f); // fog lags behind a bit

        var env = WorldEnvironment.Environment;
        env.FogDensity = currentFog;
    }

    private void ChooseWeatherForBiome(string biome)
    {
        if (!BiomeWeather.Rules.TryGetValue(biome, out var rules))
            return;

        var roll = rng.Randf();
        var acc = 0f;

        foreach (var (type, chance) in rules)
        {
            acc += chance;
            if (roll <= acc)
            {
                var newIntensity = rng.RandfRange(0.4f, 1f);
                SetWeather(type, newIntensity);
                GD.Print($"Weather changed to {type} with intensity {newIntensity} in biome {biome}");
                return;
            }
        }
    }

    private void SetWeather(WeatherType type, float intensity = 1f)
    {
        targetWeather = type;
        targetIntensity = intensity;
        AudioManager.Instance.PlayWeatherAmbience(type == WeatherType.Rain ? "rain" : null);
    }

    private void UpdateOverlay()
    {
        var viewport = GetViewport();
        var size = viewport.GetVisibleRect().Size;

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