using System.Linq;
using System.Net.Sockets;
using Godot;
using System.Threading.Tasks;

public partial class SessionBootstrap : Node
{
	private LaunchConfig Config => GetNode<LaunchConfig>("/root/LaunchConfig");
	private int _localServerPid = -1;

	private MainMenuRoot _mainMenuRoot;
	private MainMenu _mainMenu;
	private MultiplayerMenu _multiplayerMenu;
	private Control _clientUI;
	private PauseMenu _pauseMenu;

	public override async void _Ready()
	{
		switch (Config.SessionMode)
		{
			case SessionMode.None:
			case SessionMode.Menu:
				ShowMainMenu();
				break;

			case SessionMode.Single:
				await StartSinglePlayer();
				break;

			case SessionMode.Client:
				StartClient();
				break;

			case SessionMode.Server:
				StartServer();
				break;
		}

		GetWindow().Title = $"Emberwild [{Config.SessionMode}] [{Multiplayer.GetUniqueId()}]";
		GetTree().Root.CloseRequested += KillLocalServer;

		NetworkManager.Instance.ClientConnected += OnClientConnected;
		NetworkManager.Instance.ClientConnectionFailed += OnClientConnectionFailed;
		NetworkManager.Instance.ServerDisconnected += OnServerDisconnected;
	}

	private World CreateWorld()
	{
		var worldScene = GD.Load<PackedScene>("res://scenes/World.tscn");
		var world = worldScene.Instantiate<World>();

		AddChild(world);
		GameManager.Instance.AttachWorld(world);

		return world;
	}

	private async Task StartSinglePlayer()
	{
		GameManager.Instance.SessionMode = SessionMode.Single;
		StartLocalServerProcess();
		await ToSignal(GetTree().CreateTimer(1f), SceneTreeTimer.SignalName.Timeout);
		Config.OverrideAddress("127.0.0.1");
		StartClient();
	}

	private void StartServer()
	{
		var world = CreateWorld();
		GameManager.Instance.SessionMode = SessionMode.Server;

		switch (Config.WorldMode)
		{
			case WorldLoadMode.Random:
			case WorldLoadMode.Seed:
				world.InitialiseWorld(ResolveDebugSeed());
				NetworkManager.Instance.Host(Config.Port);
				break;

			case WorldLoadMode.Save:
				//load from save
				break;
		}
	}

	private int ResolveDebugSeed()
	{
		return Config.WorldMode switch
		{
			WorldLoadMode.Seed => Config.Seed,
			WorldLoadMode.Random => (int)GD.Randi(),
			WorldLoadMode.Save => -1, // placeholder until save loading
			_ => (int)GD.Randi()
		};
	}

	private void StartClient()
	{
		StartClient(Config.Address, Config.Port);
	}

	private void StartClient(string address, int port)
	{
		Config.OverrideAddress(address);
		Config.OverridePort(port);

		var err = NetworkManager.Instance.Join(Config.Address, Config.Port);
		if (err != Error.Ok)
		{
			_multiplayerMenu?.SetStatus($"Failed to start client: {err}");
			return;
		}

		_multiplayerMenu?.SetStatus($@"Connecting...");
		_multiplayerMenu?.SetConnectButtonEnabled(false);
	}

	private void OnClientConnected()
	{
		CreateWorld();
		CreateClientUI();
		GameManager.Instance.SessionMode = SessionMode.Client;

		if (IsInstanceValid(_mainMenuRoot))
			_mainMenuRoot.QueueFree();
	}

	private void OnClientConnectionFailed()
	{
		_multiplayerMenu.SetStatus($"Connection failed");
		_multiplayerMenu?.SetConnectButtonEnabled(true);
	}

	private void OnServerDisconnected()
	{
		// back to menu
	}

	private void StartLocalServerProcess()
	{
		var exePath = OS.GetExecutablePath();

		var args = new Godot.Collections.Array<string>
		{
			"--headless",
			"--debug-session=server",
			$"--port={Config.Port}",
			$"--world={Config.WorldMode.ToString().ToLower()}"
		};

		if (Config.WorldMode == WorldLoadMode.Seed)
			args.Add($"--seed={Config.Seed}");

		if (Config.WorldMode == WorldLoadMode.Save && !string.IsNullOrEmpty(Config.SaveName))
			args.Add($"--save={Config.SaveName}");

		_localServerPid = OS.CreateProcess(exePath, args.ToArray(), true);
	}

	private void ShowMainMenu()
	{
		var menuScene = GD.Load<PackedScene>("res://scenes/menus/main/MainMenuRoot.tscn");
		_mainMenuRoot = menuScene.Instantiate<MainMenuRoot>();
		AddChild(_mainMenuRoot);

		_mainMenu = _mainMenuRoot.GetNode<MainMenu>("MenuManager/MainMenu");
		_mainMenu.SingleplayerPressed += async () => await StartSinglePlayer();

		_multiplayerMenu = _mainMenuRoot.GetNode<MultiplayerMenu>("MenuManager/MultiplayerMenu");
		_multiplayerMenu.ConnectPressed += StartClient;
	}

	private void CreateClientUI()
	{
		var uiScene = GD.Load<PackedScene>("res://scenes/ui/ClientUI.tscn");
		_clientUI = uiScene.Instantiate<Control>();
		AddChild(_clientUI);

		_pauseMenu = _clientUI.GetNode<PauseMenu>("Menus/MenuManager/PauseMenu");
		_pauseMenu.OnQuitTitleRequested += OnQuitTitlePressed;
	}

	private void OnQuitTitlePressed()
	{
		GetTree().Paused = false;
		GameManager.Instance.SessionMode = SessionMode.None;
		GameManager.Instance.DetachCurrentWorld();
		GameManager.Instance.DetachLocalPlayer();
		_clientUI.QueueFree();
		OS.Kill(_localServerPid);
		_localServerPid = -1;
		ShowMainMenu();
	}

	public override void _ExitTree()
	{
		KillLocalServer();
	}

	private void KillLocalServer()
	{
		if (_localServerPid != -1)
			OS.Kill(_localServerPid);
	}
}