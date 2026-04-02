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
	}

	private World CreateWorld(int seed = -1)
	{
		var worldScene = GD.Load<PackedScene>("res://scenes/World.tscn");
		var world = worldScene.Instantiate<World>();

		var rng = new RandomNumberGenerator();
		rng.Randomize();

		if (seed == -1)
			seed = (int)rng.Randi();

		world.TerrainSeed = seed;
		AddChild(world);
		GameManager.Instance.AttachWorld(world);

		return world;
	}

	private void StartSinglePlayer()
	{
		var seed = -1;

		switch (Debug.WorldMode)
		{
			case WorldLoadMode.Random:
				seed = -1;
				break;
			case WorldLoadMode.Seed:
				seed = Debug.Seed;
				break;
			case WorldLoadMode.Save:
				// load from save
				break;
		}

		var world = CreateWorld(seed);
		GameManager.Instance.StartLocalSession(world, Vector3.Zero);
	}

	private void StartHost()
	{
		var seed = -1;

		switch (Debug.WorldMode)
		{
			case WorldLoadMode.Random:
				seed = -1;
				break;
			case WorldLoadMode.Seed:
				seed = Debug.Seed;
				break;
			case WorldLoadMode.Save:
				// load from save
				break;
		}

		var world = CreateWorld(seed);

		NetworkManager.Instance.Host(Debug.Port);
		GameManager.Instance.SpawnLocalPlayer(world, Vector3.Zero);
	}

	private async Task StartClient()
	{
		var seed = -1;

		switch (Debug.WorldMode)
		{
			case WorldLoadMode.Random:
				seed = -1;
				break;
			case WorldLoadMode.Seed:
				seed = Debug.Seed;
				break;
			case WorldLoadMode.Save:
				// load from save
				break;
		}

		var world = CreateWorld(seed);

		NetworkManager.Instance.Join(Debug.Address, Debug.Port);

		await ToSignal(GetTree().CreateTimer(0.2f), SceneTreeTimer.SignalName.Timeout);
	}

	private void ShowMainMenu()
	{
		// show main menu
	}
}