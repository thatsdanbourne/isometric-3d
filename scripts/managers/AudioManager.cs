using Godot;

public partial class AudioManager : Node
{
    public static AudioManager Instance;
    private AudioRegistry audioRegistry;

    public const string BUS_WORLD = "World";
    public const string BUS_TOOLS = "Tools";
    public const string BUS_UI = "UI";
    public const string BUS_FOOTSTEPS = "Footsteps";
    public const string BUS_AMBIENCE = "Ambience";
    public const string BUS_WEATHER = "Weather";
    public const string BUS_MUSIC = "Music";

    private AudioStreamPlayer musicPlayer;
    private float nextMusicTime;
    private readonly float musicMinDelay = 60f;
    private readonly float musicMaxDelay = 180f;

    private AudioStreamPlayer ambientA;
    private AudioStreamPlayer ambientB;
    private AudioStreamPlayer weatherPlayer;
    private float fadeSpeed = 5f;
    private string currentAmbient = "";
    private string currentWeather = "";
    private float currentIntensity = 0f;
    private Tween weatherTween;

    private float weatherMaxVolumeDb = 0f;
    private float weatherMinVolumeDb = -10f;

    private AudioStreamPlayer3D[] pool;
    private int poolIndex = 0;
    private const int POOL_SIZE = 32;

    private RandomNumberGenerator rng = new();

    public override void _Ready()
    {
        Instance = this;
        audioRegistry = new AudioRegistry();

        InitPlayers();
        InitPool();

        GameManager.Instance.DayNightCycle.DayStateChanged += OnDayStateChanged;
        GameManager.Instance.LocalPlayerChanged += OnLocalPlayerReady;

        ScheduleNextMusic();
    }

    private void InitPlayers()
    {
        musicPlayer = new AudioStreamPlayer()
        {
            Bus = BUS_MUSIC,
            VolumeDb = -6,
            Autoplay = false
        };

        AddChild(musicPlayer);

        ambientA = CreateAmbientPlayer(BUS_AMBIENCE);
        ambientB = CreateAmbientPlayer(BUS_AMBIENCE);
        weatherPlayer = CreateAmbientPlayer(BUS_WEATHER);

        AddChild(ambientA);
        AddChild(ambientB);
        AddChild(weatherPlayer);
    }

    private AudioStreamPlayer CreateAmbientPlayer(string bus)
    {
        return new AudioStreamPlayer()
        {
            Bus = bus,
            VolumeDb = -80,
            StreamPaused = true,
            Autoplay = false
        };
    }

    private void InitPool()
    {
        pool = new AudioStreamPlayer3D[POOL_SIZE];

        for (int i = 0; i < POOL_SIZE; i++)
        {
            var p = new AudioStreamPlayer3D
            {
                Bus = BUS_WORLD,
                AttenuationFilterDb = 6,
                AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.Logarithmic,
                MaxDistance = 40,
                UnitSize = 1,
                Autoplay = false
            };

            pool[i] = p;
            AddChild(p);
        }
    }

    public override void _Process(double delta)
    {
        nextMusicTime -= (float)delta;

        if (nextMusicTime <= 0f)
        {
            StartRandomMusic();
            ScheduleNextMusic();
        }
    }

    // Music

    private void ScheduleNextMusic()
    {
        nextMusicTime = rng.RandfRange(musicMinDelay, musicMaxDelay);
    }

    private void StartRandomMusic()
    {
        if (musicPlayer.Playing || audioRegistry.Music.Count == 0)
            return;

        var track = audioRegistry.Music[rng.RandiRange(0, audioRegistry.Music.Count - 1)];

        musicPlayer.Stream = track;
        musicPlayer.VolumeDb = -40;
        musicPlayer.Play();

        var tween = CreateTween();
        tween.TweenProperty(musicPlayer, "volume_db", -6, 3f).SetTrans(Tween.TransitionType.Sine);

        FadeOutTrack(track);
    }

    private async void FadeOutTrack(AudioStream track)
    {
        var fadeOutTime = 3.0f;
        double length = track.GetLength();
        double fadeStart = Mathf.Max(length - fadeOutTime, 0.1);

        await ToSignal(GetTree().CreateTimer(fadeStart), "timeout");

        var t = CreateTween();
        t.TweenProperty(musicPlayer, "volume_db", -40, fadeOutTime).SetTrans(Tween.TransitionType.Sine);

        await ToSignal(t, "finished");
        musicPlayer.Stop();
    }

    // Ambience

    public void PlayAmbience(string key, float fadeTime = 8f)
    {
        if (currentAmbient == key) return;
        currentAmbient = key;
        if (!audioRegistry.Ambiance.TryGetValue(key, out var stream)) return;

        var old = ambientA;
        ambientA = ambientB;
        ambientB = old;

        ambientB.Stream = stream;
        ambientB.VolumeDb = -40f;
        ambientB.StreamPaused = false;
        ambientB.Play();

        var tween = CreateTween();
        tween.TweenProperty(ambientA, "volume_db", -40f, fadeTime)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);

        tween.Parallel().TweenProperty(ambientB, "volume_db", 0f, fadeTime)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
    }

    public void UpdateBiomeAmbience(string biome, bool isDaytime)
    {
        if (!BiomeAmbiances.AmbianceMap.TryGetValue(biome, out var ambience))
            return;

        string key = isDaytime ? ambience.DayAmbianceKey : ambience.NightAmbianceKey;
        PlayAmbience(key);
    }

    // Weather

    public void PlayWeatherAmbience(string key, float intensity = 0f, float fadeTime = 8f)
    {
        currentIntensity = intensity;

        if (string.IsNullOrEmpty(key))
        {
            if (weatherTween != null && weatherTween.IsRunning())
                weatherTween.Kill();

            weatherTween = CreateTween();
            weatherTween.TweenProperty(weatherPlayer, "volume_db", -80, fadeTime * 2f)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.InOut);

            currentWeather = "";

            return;
        }

        if (!audioRegistry.Ambiance.TryGetValue(key, out var stream))
            return;

        if (currentWeather == key)
        {
            UpdateWeatherVolume(intensity, fadeTime * 0.5f);
            return;
        }

        currentWeather = key;

        if (weatherTween != null && weatherTween.IsRunning())
            weatherTween.Kill();

        weatherPlayer.Stream = stream;
        weatherPlayer.VolumeDb = -80;
        weatherPlayer.StreamPaused = false;
        weatherPlayer.Play();

        UpdateWeatherVolume(intensity, fadeTime);
    }

    private void UpdateWeatherVolume(float intensity, float fadeTime)
    {
        float targetVolume = Mathf.Lerp(weatherMinVolumeDb, weatherMaxVolumeDb, intensity);

        if (weatherTween != null && weatherTween.IsRunning())
            weatherTween.Kill();

        weatherTween = CreateTween();
        weatherTween.TweenProperty(weatherPlayer, "volume_db", targetVolume, fadeTime)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
    }

    // SFX

    public void PlaySfx(string key, float pitchRange = 0f, float volumeOffsetDb = 0f)
    {
        if (!audioRegistry.Sfx.TryGetValue(key, out var stream))
            return;

        var p = new AudioStreamPlayer();
        AddChild(p);

        p.Bus = BUS_WORLD;
        p.Stream = stream;
        p.PitchScale = 1f + rng.RandfRange(-pitchRange, pitchRange);
        p.VolumeDb = volumeOffsetDb;

        p.Play();
        p.Finished += p.QueueFree;
    }

    public void PlayVariant(string key)
    {
        if (!audioRegistry.SfxVariants.TryGetValue(key, out var list))
            return;

        var sfx = list[rng.RandiRange(0, list.Length - 1)];
        PlaySfxStream(sfx);
    }

    private void PlaySfxStream(AudioStream stream)
    {
        var p = new AudioStreamPlayer();
        AddChild(p);

        p.Bus = BUS_WORLD;
        p.Stream = stream;
        p.Play();
        p.Finished += p.QueueFree;
    }

    public void PlayAt(string key, Vector3 position, float pitchRange = 0.0f)
    {
        if (!audioRegistry.Sfx.TryGetValue(key, out var stream))
            return;

        var p = pool[poolIndex];
        poolIndex = (poolIndex + 1) % pool.Length;

        p.Stream = stream;
        p.PitchScale = 1.0f + rng.RandfRange(-pitchRange, pitchRange);
        p.GlobalPosition = position;
        p.Play();
    }

    public void PlayVariantAt(string key, Vector3 position, float pitchRange = 0.0f)
    {
        if (!audioRegistry.SfxVariants.TryGetValue(key, out var list) || list.Length == 0)
            return;

        // Pick a random variant
        var stream = list[rng.RandiRange(0, list.Length - 1)];

        // Use the pooled 3D players
        var p = pool[poolIndex];
        poolIndex = (poolIndex + 1) % pool.Length;

        p.Stream = stream;
        p.PitchScale = 1.0f + rng.RandfRange(-pitchRange, pitchRange);
        p.GlobalPosition = position;
        p.Play();
    }

    // Event Handlers

    private void OnLocalPlayerReady(Player p)
    {
        p.BiomeChanged += OnBiomeChanged;
    }

    private void OnBiomeChanged(string biome)
    {
        bool isDaytime = GameManager.Instance.DayNightCycle.isDaytime;
        UpdateBiomeAmbience(biome, isDaytime);
    }

    private void OnDayStateChanged(bool isDaytime)
    {
        if (GameManager.Instance.LocalPlayer == null) return;

        string biome = GameManager.Instance.LocalPlayer.CurrentBiome;
        UpdateBiomeAmbience(biome, isDaytime);
    }
}
