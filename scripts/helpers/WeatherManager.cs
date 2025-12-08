using Godot;

public partial class WeatherManager : Node
{
    public enum WeatherType { Clear, Rain, Snow }

    public WeatherType CurrentWeather = WeatherType.Snow;
    public float Intensity = 1f;

    [Export] public Node3D ParticleAnchor;
    [Export] public GpuParticles2D RainParticles;
    [Export] public GpuParticles2D SnowParticles;
    [Export] public Player Player;

    public override void _Process(double delta)
    {
        UpdateParticles();
    }

    public override void _Ready()
    {
        UpdateOverlay();
        GetViewport().Connect("size_changed", new Callable(this, nameof(UpdateOverlay)));
    }

    private void UpdateParticles()
    {
        SnowParticles.Visible = CurrentWeather == WeatherType.Snow;

        switch (CurrentWeather)
        {
            case WeatherType.Rain:
                {
                    RainParticles.Visible = true;
                    SnowParticles.Visible = false;

                    // Smooth fade in/out
                    RainParticles.Modulate = new Color(1, 1, 1, Intensity);

                    RainParticles.Amount = (int)Mathf.Lerp(150, 600, Intensity);
                    RainParticles.Emitting = true;

                    SnowParticles.Emitting = false;
                    break;
                }
            case WeatherType.Snow:
                {
                    RainParticles.Visible = false;
                    SnowParticles.Visible = true;

                    // Smooth fade in/out
                    SnowParticles.Modulate = new Color(1, 1, 1, Intensity);

                    SnowParticles.Amount = (int)Mathf.Lerp(100, 400, Intensity);
                    SnowParticles.Emitting = true;

                    RainParticles.Emitting = false;
                    break;
                }
            default:
                {
                    RainParticles.Visible = false;
                    SnowParticles.Visible = false;
                    RainParticles.Emitting = false;
                    SnowParticles.Emitting = false;
                    break;
                }
        }
    }

    public void SetWeather(WeatherType type, float intensity = 1f)
    {
        CurrentWeather = type;
        Intensity = intensity;
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
