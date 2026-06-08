using Godot;

public partial class AudioManager : Node
{
	public static AudioManager Instance;
	private AudioRegistry _audioRegistry;

	public const string BusWorld = "World";
	public const string BusTools = "Tools";
	public const string BusUI = "UI";
	public const string BusFootsteps = "Footsteps";
	private const string BusAmbience = "Ambience";
	private const string BusWeather = "Weather";
	private const string BusMusic = "Music";

	private AudioStreamPlayer _musicPlayer;
	private float _nextMusicTime;
	private readonly float _musicMinDelay = 1f;
	private readonly float _musicMaxDelay = 3f;

	private AudioStreamPlayer _ambientA;
	private AudioStreamPlayer _ambientB;
	private AudioStreamPlayer _weatherPlayer;
	private AudioStreamPlayer _footstepsPlayer;

	private float _fadeSpeed = 5f;
	private string _currentAmbient = "";
	private string _currentWeather = "";
	private float _currentIntensity;
	private Tween _weatherTween;

	private float _weatherMaxVolumeDb;
	private float _weatherMinVolumeDb = -10f;

	private AudioStreamPlayer3D[] _pool;
	private int _poolIndex;
	private const int PoolSize = 32;

	private RandomNumberGenerator _rng = new();

	public override void _Ready()
	{
		Instance = this;
		_audioRegistry = new AudioRegistry();

		InitPlayers();
		InitPool();

		GameManager.Instance.CurrentWorldChanged += OnWorldChanged;
		GameManager.Instance.LocalPlayerChanged += OnLocalPlayerChanged;

		if (GameManager.Instance.CurrentWorld != null)
			OnWorldChanged(GameManager.Instance.CurrentWorld);

		ScheduleNextMusic();
	}

	private void InitPlayers()
	{
		_musicPlayer = new AudioStreamPlayer
		{
			Bus = BusMusic,
			VolumeDb = -6,
			Autoplay = false
		};

		AddChild(_musicPlayer);

		_ambientA = CreateAmbientPlayer(BusAmbience);
		_ambientB = CreateAmbientPlayer(BusAmbience);
		_weatherPlayer = CreateAmbientPlayer(BusWeather);
		_footstepsPlayer = CreateAmbientPlayer(BusFootsteps);

		AddChild(_ambientA);
		AddChild(_ambientB);
		AddChild(_weatherPlayer);
	}

	private AudioStreamPlayer CreateAmbientPlayer(string bus)
	{
		return new AudioStreamPlayer
		{
			Bus = bus,
			VolumeDb = -80,
			StreamPaused = true,
			Autoplay = false
		};
	}

	private void InitPool()
	{
		_pool = new AudioStreamPlayer3D[PoolSize];

		for (var i = 0; i < PoolSize; i++)
		{
			var p = new AudioStreamPlayer3D
			{
				Bus = BusWorld,
				AttenuationFilterDb = 2,
				AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
				MaxDistance = 40,
				UnitSize = 4,
				Autoplay = false
			};

			_pool[i] = p;
			AddChild(p);
		}
	}

	public override void _Process(double delta)
	{
		_nextMusicTime -= (float)delta;

		if (_nextMusicTime <= 0f)
		{
			StartRandomMusic();
			ScheduleNextMusic();
		}
	}

	// Music

	private void ScheduleNextMusic()
	{
		_nextMusicTime = _rng.RandfRange(_musicMinDelay, _musicMaxDelay);
	}

	private void StartRandomMusic()
	{
		if (_musicPlayer.Playing || _audioRegistry.Music.Count == 0)
			return;

		var track = _audioRegistry.Music[_rng.RandiRange(0, _audioRegistry.Music.Count - 1)];

		_musicPlayer.Stream = track;
		_musicPlayer.VolumeDb = -40;
		_musicPlayer.Play();

		var tween = CreateTween();
		tween.TweenProperty(_musicPlayer, "volume_db", -6, 3f).SetTrans(Tween.TransitionType.Sine);

		FadeOutTrack(track);
	}

	private async void FadeOutTrack(AudioStream track)
	{
		var fadeOutTime = 3.0f;
		var length = track.GetLength();
		var fadeStart = Mathf.Max(length - fadeOutTime, 0.1);

		await ToSignal(GetTree().CreateTimer(fadeStart), "timeout");

		var t = CreateTween();
		t.TweenProperty(_musicPlayer, "volume_db", -40, fadeOutTime).SetTrans(Tween.TransitionType.Sine);

		await ToSignal(t, "finished");
		_musicPlayer.Stop();
	}

	// Ambience

	private void PlayAmbience(string key, float fadeTime = 8f)
	{
		if (_currentAmbient == key) return;
		_currentAmbient = key;
		if (!_audioRegistry.Ambiance.TryGetValue(key, out var stream)) return;

		(_ambientA, _ambientB) = (_ambientB, _ambientA);

		_ambientB.Stream = stream;
		_ambientB.VolumeDb = -40f;
		_ambientB.StreamPaused = false;
		_ambientB.Play();

		var tween = CreateTween();
		tween.TweenProperty(_ambientA, "volume_db", -40f, fadeTime)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.InOut);

		tween.Parallel().TweenProperty(_ambientB, "volume_db", 0f, fadeTime)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.InOut);
	}

	private void UpdateBiomeAmbience(BiomeId biome, bool isDaytime)
	{
		if (!BiomeAmbiances.AmbianceMap.TryGetValue(biome, out var ambience))
			return;

		var key = isDaytime ? ambience.DayAmbianceKey : ambience.NightAmbianceKey;
		PlayAmbience(key);
	}

	// Weather

	public void PlayWeatherAmbience(string key, float intensity = 0f, float fadeTime = 8f)
	{
		_currentIntensity = intensity;

		if (string.IsNullOrEmpty(key))
		{
			if (_weatherTween != null && _weatherTween.IsRunning())
				_weatherTween.Kill();

			_weatherTween = CreateTween();
			_weatherTween.TweenProperty(_weatherPlayer, "volume_db", -80, fadeTime * 2f)
				.SetTrans(Tween.TransitionType.Sine)
				.SetEase(Tween.EaseType.InOut);

			_currentWeather = "";

			return;
		}

		if (!_audioRegistry.Ambiance.TryGetValue(key, out var stream))
			return;

		if (_currentWeather == key)
		{
			UpdateWeatherVolume(intensity, fadeTime * 0.5f);
			return;
		}

		_currentWeather = key;

		if (_weatherTween != null && _weatherTween.IsRunning())
			_weatherTween.Kill();

		_weatherPlayer.Stream = stream;
		_weatherPlayer.VolumeDb = -80;
		_weatherPlayer.StreamPaused = false;
		_weatherPlayer.Play();

		UpdateWeatherVolume(intensity, fadeTime);
	}

	private void UpdateWeatherVolume(float intensity, float fadeTime)
	{
		var targetVolume = Mathf.Lerp(_weatherMinVolumeDb, _weatherMaxVolumeDb, intensity);

		if (_weatherTween != null && _weatherTween.IsRunning())
			_weatherTween.Kill();

		_weatherTween = CreateTween();
		_weatherTween.TweenProperty(_weatherPlayer, "volume_db", targetVolume, fadeTime)
			.SetTrans(Tween.TransitionType.Sine)
			.SetEase(Tween.EaseType.InOut);
	}

	// SFX

	public void PlaySfx(string key, float pitchRange = 0f, float volumeOffsetDb = 0f)
	{
		if (!_audioRegistry.Sfx.TryGetValue(key, out var stream))
			return;

		var p = new AudioStreamPlayer();
		AddChild(p);

		p.Bus = BusWorld;
		p.Stream = stream;
		p.PitchScale = 1f + _rng.RandfRange(-pitchRange, pitchRange);
		p.VolumeDb = volumeOffsetDb;

		p.Play();
		p.Finished += p.QueueFree;
	}

	public void PlayVariant(string key)
	{
		if (!_audioRegistry.SfxVariants.TryGetValue(key, out var list))
			return;

		var sfx = list[_rng.RandiRange(0, list.Length - 1)];
		PlaySfxStream(sfx);
	}

	private void PlaySfxStream(AudioStream stream)
	{
		var p = new AudioStreamPlayer();
		AddChild(p);

		p.Bus = BusWorld;
		p.Stream = stream;
		p.Play();
		p.Finished += p.QueueFree;
	}

	public void PlayAt(string key, Vector3 position, float pitchRange = 0.0f)
	{
		if (!_audioRegistry.Sfx.TryGetValue(key, out var stream))
			return;

		var p = _pool[_poolIndex];
		_poolIndex = (_poolIndex + 1) % _pool.Length;

		p.Stream = stream;
		p.PitchScale = 1.0f + _rng.RandfRange(-pitchRange, pitchRange);
		p.GlobalPosition = position;
		p.Play();
	}

	public void PlayVariantAt(string key, Vector3 position, string bus, float pitchRange = 0.0f)
	{
		if (!_audioRegistry.SfxVariants.TryGetValue(key, out var list) || list.Length == 0)
			return;

		// Pick a random variant
		var stream = list[_rng.RandiRange(0, list.Length - 1)];

		// Use the pooled 3D players
		var p = _pool[_poolIndex];
		_poolIndex = (_poolIndex + 1) % _pool.Length;

		p.Stream = stream;
		p.Bus = bus;
		p.PitchScale = 1.0f + _rng.RandfRange(-pitchRange, pitchRange);
		p.GlobalPosition = position;
		p.Play();
	}

	// Event Handlers

	private void OnWorldChanged(World world)
	{
		if (world == null)
			return;

		world.DayNightCycle.DayStateChanged += OnDayStateChanged;
	}

	private void OnLocalPlayerChanged()
	{
		GameManager.Instance.LocalPlayer.BiomeChanged += OnBiomeChanged;
	}

	private void OnBiomeChanged(BiomeId biome)
	{
		var isDaytime = GameManager.Instance.DayNightCycle.IsDaytime;
		UpdateBiomeAmbience(biome, isDaytime);
	}

	private void OnDayStateChanged(bool isDaytime)
	{
		if (GameManager.Instance.LocalPlayer == null) return;

		var biome = GameManager.Instance.LocalPlayer.CurrentBiome;
		UpdateBiomeAmbience(biome, isDaytime);
	}
}