using Godot;
using System;
using System.Threading.Tasks;

public partial class SessionBootstrap : Node
{
	private LaunchConfig Config => GetNode<LaunchConfig>("/root/LaunchConfig");

	public override async void _Ready()
	{
		switch (Config.SessionMode)
		{
			case SessionMode.None:
			case SessionMode.Menu:
				ShowMainMenu();
				break;

			case SessionMode.Single:
				StartSinglePlayer();
				break;

			case SessionMode.Host:
				StartHost();
				break;

			case SessionMode.Client:
				await StartClient();
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

	private void StartSinglePlayer()
	{
		var world = CreateWorld();
		GameManager.Instance.SessionMode = SessionMode.Single;
		switch (Config.WorldMode)
		{
			case WorldLoadMode.Random:
			case WorldLoadMode.Seed:
				world.InitialiseWorld(ResolveDebugSeed());
				GameManager.Instance.StartLocalSession(world, Vector3.Zero);
				break;

			case WorldLoadMode.Save:
				// load from save
				break;
		}
	}

	private void StartHost()
	{
		var world = CreateWorld();

		switch (Config.WorldMode)
		{
			case WorldLoadMode.Random:
			case WorldLoadMode.Seed:
				world.InitialiseWorld(ResolveDebugSeed());
				GameManager.Instance.StartLocalSession(world, Vector3.Zero);
				NetworkManager.Instance.Host(Config.Port);
				break;

			case WorldLoadMode.Save:
				// load from save
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
		var world = CreateWorld();

		NetworkManager.Instance.Join(Config.Address, Config.Port);

		await ToSignal(GetTree().CreateTimer(0.2f), SceneTreeTimer.SignalName.Timeout);
	}

	private void ShowMainMenu()
	{
		// show main menu
	}
}