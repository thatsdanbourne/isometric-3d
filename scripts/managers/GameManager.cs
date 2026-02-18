using Godot;

public partial class GameManager : Node
{
	public static GameManager Instance;
	public World World;

	[Signal]
	public delegate void LocalPlayerChangedEventHandler(Player p);

	public PackedScene PlayerScene = GD.Load<PackedScene>("res://scenes/player/Player.tscn");

	public DayNightCycle DayNightCycle { get; private set; }
	public WeatherManager WeatherManager { get; private set; }

	public Player LocalPlayer { get; private set; }
	private bool _spawningPlayer;

	public override void _Ready()
	{
		Instance = this;

		DayNightCycle = GetNode<DayNightCycle>("/root/Game//World/DayNightCycle");
		WeatherManager = GetNode<WeatherManager>("/root/Game//World/WeatherManager");

		GD.Print("GameManager initialized.");
	}

	public void SetLocalPlayer(Player p)
	{
		LocalPlayer = p;
		p.BiomeChanged += WeatherManager.SetBiome;
		EmitSignal(SignalName.LocalPlayerChanged, p);
	}

	public void SpawnLocalPlayer()
	{
		if (_spawningPlayer)
		{
			GD.PrintErr("Local player is already being spawned.");
			return;
		}

		_spawningPlayer = true;

		if (World == null)
		{
			GD.PrintErr("World is not registered in GameManager.");
			return;
		}

		var player = PlayerScene.Instantiate<Player>();
		World.PlayerContainer.AddChild(player);
		player.GlobalPosition = Vector3.Zero;
		SetLocalPlayer(player);
		_spawningPlayer = false;
	}

	public void RegisterWorld(World world)
	{
		World = world;
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