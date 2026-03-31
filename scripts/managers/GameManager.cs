using Godot;

public partial class GameManager : Node
{
	public static GameManager Instance { get; private set; }

	[Signal]
	public delegate void LocalPlayerChangedEventHandler(Player p);

	[Signal]
	public delegate void CurrentWorldChangedEventHandler(World world);

	public World CurrentWorld { get; private set; }
	public Player LocalPlayer { get; private set; }

	public PackedScene PlayerScene = GD.Load<PackedScene>("res://scenes/player/Player.tscn");

	public DayNightCycle DayNightCycle { get; private set; }
	public WeatherManager WeatherManager { get; private set; }

	private bool _spawningPlayer;

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _Ready()
	{
		// DayNightCycle = GetNode<DayNightCycle>("/root/Game//World/DayNightCycle");
		// WeatherManager = GetNode<WeatherManager>("/root/Game//World/WeatherManager");

		GD.Print("GameManager initialized.");
	}

	public void AttachWorld(World world)
	{
		if (CurrentWorld == world) return;

		if (CurrentWorld != null) DetatchWorld(CurrentWorld);

		CurrentWorld = world;
		DayNightCycle = world.DayNightCycle;
		WeatherManager = world.WeatherManager;

		EmitSignal(SignalName.CurrentWorldChanged, world);
		if (LocalPlayer == null || WeatherManager == null)
			return;

		LocalPlayer.BiomeChanged -= WeatherManager.SetBiome;
		LocalPlayer.BiomeChanged += WeatherManager.SetBiome;
	}

	public void DetatchWorld(World world)
	{
		if (CurrentWorld != world) return;

		if (LocalPlayer == null || WeatherManager == null)
			return;

		LocalPlayer.BiomeChanged -= WeatherManager.SetBiome;

		CurrentWorld = null;
		DayNightCycle = null;
		WeatherManager = null;
		EmitSignal(SignalName.CurrentWorldChanged, (World)null);
	}

	public void AttachLocalPlayer(Player player)
	{
		if (LocalPlayer == player) return;

		if (LocalPlayer != null)
			LocalPlayer.BiomeChanged -= WeatherManager.SetBiome;

		LocalPlayer = player;

		if (LocalPlayer != null)
			LocalPlayer.BiomeChanged += WeatherManager.SetBiome;

		EmitSignal(SignalName.LocalPlayerChanged, player);
	}

	public void DetatchLocalPlayer(Player player)
	{
		if (LocalPlayer != player) return;

		LocalPlayer.BiomeChanged -= WeatherManager.SetBiome;
		LocalPlayer = null;

		EmitSignal(SignalName.LocalPlayerChanged, (Player)null);
	}

	public Player SpawnLocalPlayer(World world, Vector3 spawnPosition)
	{
		if (_spawningPlayer)
		{
			GD.PrintErr("Local player is already being spawned.");
			return null;
		}

		if (world == null)
		{
			GD.PrintErr("Cannot spawn local player: world is null.");
			return null;
		}

		_spawningPlayer = true;

		try
		{
			var player = CreateLocalPlayer();
			world.AddPlayer(player, spawnPosition);
			AttachLocalPlayer(player);
			return player;
		}
		finally
		{
			_spawningPlayer = false;
		}
	}

	public void StartLocalSession(World world, Vector3 spawnPosition)
	{
		AttachWorld(world);
		world.ChunkManager.PreloadChunks(spawnPosition);
		var player = SpawnLocalPlayer(world, spawnPosition);
		player.CheckBiome();
	}

	private Player CreateLocalPlayer()
	{
		return PlayerScene.Instantiate<Player>();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("toggle_fullscreen"))
		{
			ToggleFullscreen();
			GetViewport().SetInputAsHandled();
		}
	}

	private void ToggleFullscreen()
	{
		SettingsManager.Instance.Fullscreen = !SettingsManager.Instance.Fullscreen;
		SettingsManager.Instance.ApplyAll();
	}
}