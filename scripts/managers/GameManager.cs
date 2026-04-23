using Godot;

public partial class GameManager : Node
{
	public static GameManager Instance { get; private set; }

	[Signal]
	public delegate void LocalPlayerChangedEventHandler(Player p);

	[Signal]
	public delegate void CurrentWorldChangedEventHandler(World world);

	public SessionMode SessionMode { get; set; }

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
		GD.Print("GameManager initialized.");
	}

	public void AttachWorld(World world)
	{
		if (CurrentWorld == world) return;

		if (CurrentWorld != null) DetachCurrentWorld();

		CurrentWorld = world;
		DayNightCycle = world.DayNightCycle;
		WeatherManager = world.WeatherManager;

		EmitSignal(SignalName.CurrentWorldChanged, world);
		if (LocalPlayer == null || WeatherManager == null)
			return;

		LocalPlayer.BiomeChanged -= WeatherManager.SetBiome;
		LocalPlayer.BiomeChanged += WeatherManager.SetBiome;
	}

	public void DetachCurrentWorld()
	{
		if (CurrentWorld == null)
			return;

		if (LocalPlayer != null && WeatherManager != null)
			LocalPlayer.BiomeChanged -= WeatherManager.SetBiome;

		CurrentWorld.QueueFree();
		CurrentWorld = null;
		DayNightCycle = null;
		WeatherManager = null;
		EmitSignal(SignalName.CurrentWorldChanged, (World)null);
	}

	public void AttachLocalPlayer(Player player)
	{
		if (player == null)
		{
			if (LocalPlayer != null && WeatherManager != null)
				LocalPlayer.BiomeChanged -= WeatherManager.SetBiome;

			LocalPlayer = null;
			EmitSignal(SignalName.LocalPlayerChanged, (Player)null);
			return;
		}

		var uniqueId = Multiplayer.HasMultiplayerPeer() ? Multiplayer.GetUniqueId() : 0;
		var isOfflineLocal = player.PlayerId == 0;
		var isNetworkLocal = player.PlayerId == uniqueId;

		if (!isOfflineLocal && !isNetworkLocal)
		{
			GD.PrintErr($"Refusing to attach non-local player {player.PlayerId} on peer {uniqueId}");
			return;
		}

		if (LocalPlayer == player)
			return;

		if (LocalPlayer != null && WeatherManager != null)
			LocalPlayer.BiomeChanged -= WeatherManager.SetBiome;

		LocalPlayer = player;

		if (WeatherManager != null)
			LocalPlayer.BiomeChanged += WeatherManager.SetBiome;

		EmitSignal(SignalName.LocalPlayerChanged, player);
	}

	public void DetachLocalPlayer()
	{
		if (LocalPlayer != null && WeatherManager != null)
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
			player.Name = "Player_Local";
			player.PlayerId = 0;
			player.IsLocal = true;

			world.AddPlayer(player, spawnPosition);
			AttachLocalPlayer(player);
			return player;
		}
		finally
		{
			_spawningPlayer = false;
		}
	}

	public void PromoteLocalPlayerToHost()
	{
		if (LocalPlayer == null || !IsInstanceValid(LocalPlayer))
		{
			GD.PrintErr("No local player to promote to host");
			return;
		}

		LocalPlayer.PlayerId = Multiplayer.GetUniqueId();
		LocalPlayer.Name = $"Player_{LocalPlayer.PlayerId}";
		LocalPlayer.IsLocal = true;
		SessionMode = SessionMode.Host;
	}

	public void StartLocalSession(World world, Vector3 spawnPosition)
	{
		var player = SpawnLocalPlayer(world, spawnPosition);
		player.CheckBiome();
	}

	private Player CreateLocalPlayer()
	{
		return PlayerScene.Instantiate<Player>();
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	public void RequestSpawnPlayer(int peerId)
	{
		if (!Multiplayer.IsServer())
			return;

		if (CurrentWorld == null)
		{
			GD.PrintErr("No current world to spawn player into.");
			return;
		}

		var spawnPos = Vector3.Zero;
		Rpc(nameof(SpawnPlayerReplica), peerId, spawnPos);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true)]
	private void SpawnPlayerReplica(int peerId, Vector3 spawnPos)
	{
		if (CurrentWorld == null)
		{
			GD.PrintErr("No current world to spawn player into");
			return;
		}

		if (CurrentWorld.HasPlayer(peerId))
			return;

		var player = CreateLocalPlayer();
		player.Name = $"Player_{peerId}";
		player.PlayerId = peerId;
		player.IsLocal = peerId == Multiplayer.GetUniqueId();

		CurrentWorld.AddPlayer(player, spawnPos);

		if (player.IsLocal)
		{
			ForceSingleLocalPlayer(player);
			AttachLocalPlayer(player);
		}

		GD.Print($"Spawned player replica {peerId}, local={player.IsLocal}, peer={Multiplayer.GetUniqueId()}");
	}

	private void ForceSingleLocalPlayer(Player actualLocalPlayer)
	{
		if (CurrentWorld == null)
			return;

		foreach (var player in CurrentWorld.Players)
		{
			if (player == null || !IsInstanceValid(player))
				continue;

			player.IsLocal = player == actualLocalPlayer;
		}
	}

	public void SyncExistingPlayersToPeer(int peerId)
	{
		if (!Multiplayer.IsServer() || CurrentWorld == null)
			return;

		foreach (var player in CurrentWorld.Players)
		{
			if (player == null || !IsInstanceValid(player) || !player.IsInsideTree())
				continue;

			RpcId(peerId, nameof(SpawnPlayerReplica), player.PlayerId, player.GlobalPosition);
			CurrentWorld.Sync.SyncHeldItemToPeer(peerId, player);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	public void RequestInitialJoinState()
	{
		if (!Multiplayer.IsServer() || CurrentWorld == null)
			return;

		var peerId = Multiplayer.GetRemoteSenderId();

		RequestSpawnPlayer(peerId);
		SendWorldInitToPeer(peerId);
		SyncExistingPlayersToPeer(peerId);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	public void ReceiveWorldInit(int terrainSeed, double worldTimeSeconds)
	{
		if (CurrentWorld == null)
		{
			GD.PrintErr("No current world to initialise");
			return;
		}

		CurrentWorld.InitialiseWorld(terrainSeed, worldTimeSeconds);

		GD.Print($"Received world init: Seed={terrainSeed}");
	}

	public void SendWorldInitToPeer(int peerId)
	{
		if (!Multiplayer.IsServer() || CurrentWorld == null)
			return;

		RpcId(peerId, nameof(ReceiveWorldInit), CurrentWorld.TerrainSeed, CurrentWorld.WorldTimeSeconds);

		var weatherState = WeatherManager.GetCurrentState();
		CurrentWorld.Sync.SendWeatherStateToPeer(peerId, weatherState);

		CurrentWorld.ItemDropManager.SendNearbyPickupsToPeer(
			peerId,
			CurrentWorld.GetPlayerById(peerId).GlobalPosition,
			64f
		);
	}

	public void PeerDisconnected(int peerId)
	{
		var player = CurrentWorld?.GetPlayerById(peerId);
		if (player == null)
			return;

		if (player.IsLocal) DetachLocalPlayer();

		CurrentWorld.RemovePlayer(player);
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