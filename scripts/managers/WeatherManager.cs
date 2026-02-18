using Godot;

public partial class WeatherManager : Node
{
	[Export] public Node3D ParticleAnchor;
	[Export] public GpuParticles2D RainParticles;
	[Export] public GpuParticles2D SnowParticles;
	[Export] public WorldEnvironment WorldEnvironment;

	private WeatherType _currentWeather = WeatherType.Rain;
	private float _currentIntensity = 0.5f;

	private WeatherType _targetWeather;
	private float _targetIntensity;
	private float _weatherTransitionSpeed = 0.3f;
	private float _fadeLerpSpeed = 0.0005f;

	private float _weatherTimer;
	private float _nextWeatherChange = 20f;

	private float _rainFade;
	private float _snowFade;

	private float _rainSunDim = 0.75f;
	private float _snowSunDim = 0.85f;

	private float _baseFog;
	private float _rainFog = 0.3f;
	private float _snowFog = 0.5f;

	private float SunlightMultiplier { get; set; } = 1f;

	private float _currentFog;

	private string _currentBiome = "";

	private RandomNumberGenerator _rng = new();


	public override void _Ready()
	{
		_targetWeather = _currentWeather;
		_rng.Randomize();

		UpdateOverlay();
		GetViewport().Connect("size_changed", new Callable(this, nameof(UpdateOverlay)));
	}


	public override void _Process(double delta)
	{
		var dt = (float)delta;

		_weatherTimer += dt;
		if (_weatherTimer >= _nextWeatherChange)
		{
			_weatherTimer = 0f;
			_nextWeatherChange = _rng.RandfRange(60f, 180f);

			if (!string.IsNullOrEmpty(_currentBiome))
				ChooseWeatherForBiome(_currentBiome);
		}

		_currentIntensity = Mathf.Lerp(_currentIntensity, _targetIntensity, dt * _weatherTransitionSpeed);

		UpdateWeather();
		UpdateLightingAndFog(dt);
	}

	public void SetBiome(string biome)
	{
		if (biome == _currentBiome) return;

		_currentBiome = biome;
		_weatherTimer = 0f;
		_nextWeatherChange = _rng.RandfRange(10f, 30f);

		ChooseWeatherForBiome(biome);
	}

	private void UpdateWeather()
	{
		var rainTarget =
			_targetWeather == WeatherType.Rain ? _targetIntensity : 0f;

		var snowTarget =
			_targetWeather == WeatherType.Snow ? _targetIntensity : 0f;


		_rainFade = Mathf.Lerp(_rainFade, rainTarget, _fadeLerpSpeed);
		_snowFade = Mathf.Lerp(_snowFade, snowTarget, _fadeLerpSpeed);


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


		if (_rainFade < 0.02f && _snowFade < 0.02f)
			_currentWeather = WeatherType.Clear;
		else if (_rainFade > _snowFade)
			_currentWeather = WeatherType.Rain;
		else
			_currentWeather = WeatherType.Snow;
	}

	private void UpdateLightingAndFog(float dt)
	{
		float targetMultiplier;

		switch (_targetWeather)
		{
			case WeatherType.Rain:
				targetMultiplier = Mathf.Lerp(1f, _rainSunDim, _targetIntensity);
				break;

			case WeatherType.Snow:
				targetMultiplier = Mathf.Lerp(1f, _snowSunDim, _targetIntensity);
				break;

			case WeatherType.Clear:
			default:
				targetMultiplier = 1f;
				break;
		}

		SunlightMultiplier = Mathf.Lerp(SunlightMultiplier, targetMultiplier, dt * 0.5f);

		if (WorldEnvironment == null) return;

		float targetFog;

		switch (_targetWeather)
		{
			case WeatherType.Rain:
				targetFog = Mathf.Lerp(_baseFog, _rainFog, _targetIntensity);
				break;

			case WeatherType.Snow:
				targetFog = Mathf.Lerp(_baseFog, _snowFog, _targetIntensity);
				break;

			case WeatherType.Clear:
			default:
				targetFog = _baseFog;
				break;
		}

		_currentFog = Mathf.Lerp(_currentFog, targetFog, _fadeLerpSpeed * 0.75f); // fog lags behind a bit

		var env = WorldEnvironment.Environment;
		env.FogDensity = _currentFog;
	}

	private void ChooseWeatherForBiome(string biome)
	{
		if (!BiomeWeather.Rules.TryGetValue(biome, out var rules))
			return;

		var roll = _rng.Randf();
		var acc = 0f;

		foreach (var (type, chance) in rules)
		{
			acc += chance;
			if (roll <= acc)
			{
				var newIntensity = _rng.RandfRange(0.4f, 1f);
				SetWeather(type, newIntensity);
				GD.Print($"Weather changed to {type} with intensity {newIntensity} in biome {biome}");
				return;
			}
		}
	}

	private void SetWeather(WeatherType type, float intensity = 1f)
	{
		_targetWeather = type;
		_targetIntensity = intensity;
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