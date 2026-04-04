using Godot;
using System;
using System.Threading.Tasks;

public partial class SessionBootstrap : Node
{
	private DebugLaunchConfig Debug => GetNode<DebugLaunchConfig>("/root/DebugLaunchConfig");

	public override async void _Ready()
	{
		switch (Debug.SessionMode)
		{
			case DebugSessionMode.None:
			case DebugSessionMode.Menu:
				ShowMainMenu();
				break;

			case DebugSessionMode.Single:
				StartSinglePlayer();
				break;

			case DebugSessionMode.Host:
				StartHost();
				break;

			case DebugSessionMode.Client:
				await StartClient();
				break;
		}

		GetWindow().Title = $"Emberwild [{Debug.SessionMode}] [{Multiplayer.GetUniqueId()}]";
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

		switch (Debug.WorldMode)
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

		switch (Debug.WorldMode)
		{
			case WorldLoadMode.Random:
			case WorldLoadMode.Seed:
				world.InitialiseWorld(ResolveDebugSeed());
				GameManager.Instance.StartLocalSession(world, Vector3.Zero);
				NetworkManager.Instance.Host(Debug.Port);
				break;

			case WorldLoadMode.Save:
				// load from save
				break;
		}
	}

	private int ResolveDebugSeed()
	{
		return Debug.WorldMode switch
		{
			WorldLoadMode.Seed => Debug.Seed,
			WorldLoadMode.Random => (int)GD.Randi(),
			WorldLoadMode.Save => -1, // placeholder until save loading
			_ => (int)GD.Randi()
		};
	}

	private async Task StartClient()
	{
		var world = CreateWorld();

		NetworkManager.Instance.Join(Debug.Address, Debug.Port);

		await ToSignal(GetTree().CreateTimer(0.2f), SceneTreeTimer.SignalName.Timeout);
	}

	private void ShowMainMenu()
	{
		// show main menu
	}
}