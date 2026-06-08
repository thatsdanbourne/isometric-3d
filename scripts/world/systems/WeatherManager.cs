using Godot;

public partial class WeatherManager : Node
{
	[Export] public Node3D ParticleAnchor;
	[Export] public GpuParticles2D RainParticles;
	[Export] public GpuParticles2D SnowParticles;
	[Export] public WorldEnvironment WorldEnvironment;

	[Export] public float MinWeatherDuration = 10f;
	[Export] public float MaxWeatherDuration = 30f;
	[Export] public double TransitionDuration = 8.0;

	private World _world;

	public WeatherType CurrentWeather { get; private set; } = WeatherType.Clear;
	public float CurrentIntensity { get; private set; }

	private WeatherState _state;

	private float _weatherTimer;
	private float _nextWeatherChange = 20f;

	private float _rainFade;
	private float _snowFade;
	private float _currentFog;

	private float _prevRainFade;
	private float _prevSnowFade;
	private float _prevFog;
	private double _lastTransitionStartTime;

	private float _rainSunDim = 0.75f;
	private float _snowSunDim = 0.85f;

	private float _baseFog;
	private float _rainFog = 0.3f;
	private float _snowFog = 0.5f;

	private BiomeId _currentBiome = BiomeId.Unknown;
	private WeatherType _lastAudioWeather = WeatherType.Clear;

	private readonly RandomNumberGenerator _rng = new();

	public float SunlightMultiplier { get; private set; } = 1f;


	public override void _Ready()
	{
		_world = GetParent<World>();

		_rng.Randomize();

		_state = new WeatherState
		{
			Weather = GlobalWeatherType.Clear,
			Intensity = 0f,
			TransitionStartWorldTime = 0,
			TransitionDuration = 0
		};

		UpdateOverlay();
		GetViewport().Connect("size_changed", new Callable(this, nameof(UpdateOverlay)));
	}


	public override void _Process(double delta)
	{
		var dt = (float)delta;

		if (_world.Multiplayer.IsServer())
			ProcessAuthority(dt);

		UpdateWeatherVisuals();
		UpdateLightingAndFog();
		UpdateAudio();
	}

	private void ProcessAuthority(float dt)
	{
		_weatherTimer += dt;

		if (_weatherTimer < _nextWeatherChange)
			return;

		_weatherTimer = 0f;
		_nextWeatherChange = _rng.RandfRange(MinWeatherDuration, MaxWeatherDuration);

		ChooseNextGlobalWeather();
	}

	#region Server Weather

	private void ChooseNextGlobalWeather()
	{
		var roll = _rng.Randf();
		if (roll < 0.6f)
		{
			SetGlobalWeather(GlobalWeatherType.Clear, 0f, TransitionDuration);
		}
		else
		{
			var intensity = _rng.RandfRange(0.4f, 1f);
			SetGlobalWeather(GlobalWeatherType.Precipitation, intensity, TransitionDuration);
		}
	}

	private void SetGlobalWeather(GlobalWeatherType type, float intensity, double duration)
	{
		if (!_world.Multiplayer.IsServer())
			return;

		_state = new WeatherState
		{
			Weather = type,
			Intensity = intensity,
			TransitionStartWorldTime = _world.WorldTimeSeconds,
			TransitionDuration = duration
		};

		ApplyWeatherState(_state);

		_world.Sync.SendWeatherState(_state);

		GD.Print($"Global weather changed to {type} with intensity {intensity:0.00}");
	}

	public void ApplyWeatherState(WeatherState state)
	{
		_prevRainFade = _rainFade;
		_prevSnowFade = _snowFade;
		_prevFog = _currentFog;
		_lastTransitionStartTime = state.TransitionStartWorldTime;

		_state = state;
	}

	public WeatherState GetCurrentState()
	{
		return _state;
	}

	#endregion

	#region Client Weather

	public void SetBiome(BiomeId biome)
	{
		_currentBiome = biome;
	}

	private WeatherType ResolveLocalWeather(GlobalWeatherType globalWeather, BiomeId biome)
	{
		return globalWeather switch
		{
			GlobalWeatherType.Clear => WeatherType.Clear,
			GlobalWeatherType.Precipitation => biome switch
			{
				BiomeId.Desert => WeatherType.Clear,
				BiomeId.Tundra => WeatherType.Snow,
				BiomeId.Taiga => WeatherType.Snow,
				_ => WeatherType.Rain
			},
			_ => WeatherType.Clear
		};
	}

	#endregion

	#region Update Visuals

	private void UpdateWeatherVisuals()
	{
		var t = GetTransitionAlpha();

		var localWeather = ResolveLocalWeather(_state.Weather, _currentBiome);
		var localIntensity = _state.Intensity * t;

		var rainTarget = localWeather == WeatherType.Rain ? localIntensity : 0f;
		var snowTarget = localWeather == WeatherType.Snow ? localIntensity : 0f;

		_rainFade = Mathf.Lerp(_prevRainFade, rainTarget, t);
		_snowFade = Mathf.Lerp(_prevSnowFade, snowTarget, t);

		CurrentWeather = localWeather;
		CurrentIntensity = localIntensity;

		var rainActive = _rainFade > 0.01f;
		RainParticles.Visible = rainActive;
		RainParticles.Emitting = rainActive;
		RainParticles.Modulate = new Color(1, 1, 1, _rainFade);
		RainParticles.AmountRatio = _rainFade;

		var snowActive = _snowFade > 0.01f;
		SnowParticles.Visible = snowActive;
		SnowParticles.Emitting = snowActive;
		SnowParticles.Modulate = new Color(1, 1, 1, _snowFade);
		SnowParticles.AmountRatio = _snowFade;
	}

	private void UpdateLightingAndFog()
	{
		var targetMultiplier = CurrentWeather switch
		{
			WeatherType.Rain => Mathf.Lerp(1f, _rainSunDim, _rainFade),
			WeatherType.Snow => Mathf.Lerp(1f, _snowSunDim, _snowFade),
			_ => 1f
		};

		SunlightMultiplier = targetMultiplier;

		if (WorldEnvironment?.Environment == null)
			return;

		var targetFog = CurrentWeather switch
		{
			WeatherType.Rain => Mathf.Lerp(_baseFog, _rainFog, _rainFade),
			WeatherType.Snow => Mathf.Lerp(_baseFog, _snowFog, _snowFade),
			_ => _baseFog
		};

		var t = GetTransitionAlpha();
		_currentFog = Mathf.Lerp(_prevFog, targetFog, t);

		WorldEnvironment.Environment.FogDensity = _currentFog;
	}

	private void UpdateAudio()
	{
		var audioWeather = CurrentWeather == WeatherType.Rain ? WeatherType.Rain : WeatherType.Clear;

		if (audioWeather == _lastAudioWeather)
			return;

		_lastAudioWeather = audioWeather;
		AudioManager.Instance.PlayWeatherAmbience(audioWeather == WeatherType.Rain ? "rain" : null);
	}

	private void UpdateOverlay()
	{
		var viewport = GetViewport();
		var size = viewport.GetVisibleRect().Size;

		var rainMat = (ParticleProcessMaterial)RainParticles.ProcessMaterial;
		if (rainMat != null)
			rainMat.EmissionBoxExtents = new Vector3(size.X / 2f, 0, 0);

		RainParticles.Position = new Vector2(size.X / 2f, 0);

		var snowMat = (ParticleProcessMaterial)SnowParticles.ProcessMaterial;
		if (snowMat != null)
			snowMat.EmissionBoxExtents = new Vector3(size.X / 2f, 0, 0);

		SnowParticles.Position = new Vector2(size.X / 2f, 0);
	}

	private float GetTransitionAlpha()
	{
		if (_state.TransitionDuration <= 0)
			return 1f;

		return Mathf.Clamp(
			(float)((_world.WorldTimeSeconds - _state.TransitionStartWorldTime) / _state.TransitionDuration),
			0f,
			1f
		);
	}

	#endregion
}

public struct WeatherState
{
	public GlobalWeatherType Weather;
	public float Intensity;
	public double TransitionStartWorldTime;
	public double TransitionDuration;
}