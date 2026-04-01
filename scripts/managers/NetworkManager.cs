using Godot;
using System;

public partial class NetworkManager : Node
{
	public static NetworkManager Instance;

	public bool IsServer => Multiplayer.IsServer();
	public int LocalPeerId => Multiplayer.GetUniqueId();

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _Ready()
	{
		Multiplayer.PeerConnected += OnPeerConnected;
		Multiplayer.PeerDisconnected += OnPeerDisconnected;
		Multiplayer.ConnectedToServer += OnConnectedToServer;
		Multiplayer.ConnectionFailed += OnConnectionFailed;
		Multiplayer.ServerDisconnected += OnServerDisconnected;
	}

	public void Host(int port = 7777)
	{
		var peer = new ENetMultiplayerPeer();
		var err = peer.CreateServer(port);
		if (err != Error.Ok)
		{
			GD.PrintErr("Failed to create server: " + err);
			return;
		}

		Multiplayer.MultiplayerPeer = peer;
		GD.Print("Server started on port " + port);

		var localPlayer = GameManager.Instance.LocalPlayer;
		if (localPlayer != null)
		{
			localPlayer.PlayerId = Multiplayer.GetUniqueId();
			localPlayer.Name = $"Player_{localPlayer.PlayerId}";
			localPlayer.IsLocal = true;

			GameManager.Instance.AttachLocalPlayer(localPlayer);
		}
	}

	public void Join(string address, int port = 7777)
	{
		var peer = new ENetMultiplayerPeer();
		var err = peer.CreateClient(address, port);
		if (err != Error.Ok)
		{
			GD.PrintErr("Failed to join: " + err);
			return;
		}

		// Tear down standalone local player before joining shared session
		var localPlayer = GameManager.Instance.LocalPlayer;
		if (localPlayer != null && IsInstanceValid(localPlayer))
		{
			GameManager.Instance.DetachLocalPlayer();
			GameManager.Instance.CurrentWorld?.RemovePlayer(localPlayer);
			localPlayer.QueueFree();
		}

		Multiplayer.MultiplayerPeer = peer;
		GD.Print($"Joining {address}:{port}");
	}

	private void OnPeerConnected(long id)
	{
		GD.Print($"Peer connected: {id}");

		if (Multiplayer.IsServer())
			GameManager.Instance.SyncExistingPlayersToPeer((int)id);
	}

	private void OnPeerDisconnected(long id)
	{
		GD.Print($"Peer disconnected: {id}");
		GameManager.Instance.PeerDisconnected((int)id);
	}

	private void OnConnectedToServer()
	{
		GD.Print("Connected to server");
		GameManager.Instance.RpcId(1, nameof(GameManager.RequestSpawnPlayer), Multiplayer.GetUniqueId());
	}

	private void OnConnectionFailed()
	{
		GD.PrintErr("Connection failed");
	}

	private void OnServerDisconnected()
	{
		GD.PrintErr("Server disconnected");
	}
}