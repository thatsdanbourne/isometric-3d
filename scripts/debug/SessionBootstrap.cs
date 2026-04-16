using System.Linq;
using Godot;
using System.Threading.Tasks;

public partial class SessionBootstrap : Node
{
	private LaunchConfig Config => GetNode<LaunchConfig>("/root/LaunchConfig");
	private int _localServerPid = -1;

	public override async void _Ready()
	{
		switch (Config.SessionMode)
		{
			case SessionMode.None:
			case SessionMode.Menu:
				ShowMainMenu();
				break;

			case SessionMode.Single:
				CreateClientUI();
				await StartSinglePlayer();
				break;

			// case SessionMode.Host:
			// 	CreateClientUI();
			// 	StartHost();
			// 	break;

			case SessionMode.Client:
				CreateClientUI();
				await StartClient();
				break;

			case SessionMode.Server:
				StartServer();
				break;
		}

		GetWindow().Title = $"Emberwild [{Config.SessionMode}] [{Multiplayer.GetUniqueId()}]";
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
		await StartClient();
	}

	// private void StartHost()
	// {
	// 	var world = CreateWorld();
	//
	// 	switch (Config.WorldMode)
	// 	{
	// 		case WorldLoadMode.Random:
	// 		case WorldLoadMode.Seed:
	// 			world.InitialiseWorld(ResolveDebugSeed());
	// 			GameManager.Instance.StartLocalSession(world, Vector3.Zero);
	// 			NetworkManager.Instance.Host(Config.Port);
	// 			GameManager.Instance.PromoteLocalPlayerToHost();
	// 			break;
	//
	// 		case WorldLoadMode.Save:
	// 			// load from save
	// 			break;
	// 	}
	// }

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

	private async Task StartClient()
	{
		CreateWorld();
		GameManager.Instance.SessionMode = SessionMode.Client;
		NetworkManager.Instance.Join(Config.Address, Config.Port);

		await ToSignal(GetTree().CreateTimer(0.2f), SceneTreeTimer.SignalName.Timeout);
	}

	private void StartLocalServerProcess()
	{
		var exePath = OS.GetExecutablePath();

		var serverCommand =
			$"\"{exePath}\" --debug-session=server --port={Config.Port} --world={Config.WorldMode.ToString().ToLower()}";

		if (Config.WorldMode == WorldLoadMode.Seed)
			serverCommand += $" --seed={Config.Seed}";

		if (Config.WorldMode == WorldLoadMode.Save && !string.IsNullOrEmpty(Config.SaveName))
			serverCommand += $" --save={Config.SaveName}";

		// Launch in new terminal window
		OS.CreateProcess("cmd.exe", [
			"/k", // keep window open
			serverCommand
		]);
	}

	private void ShowMainMenu()
	{
		// show main menu
	}

	private void CreateClientUI()
	{
		var uiScene = GD.Load<PackedScene>("res://scenes/ui/ClientUI.tscn");
		var ui = uiScene.Instantiate();
		AddChild(ui);
	}

	public override void _ExitTree()
	{
		if (_localServerPid != -1)
			OS.Kill(_localServerPid);
	}
}