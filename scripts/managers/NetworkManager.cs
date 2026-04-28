using Godot;
using System;

public partial class NetworkManager : Node
{
	public static NetworkManager Instance;

	public bool IsServer => Multiplayer.IsServer();
	public int LocalPeerId => Multiplayer.GetUniqueId();

	[Signal]
	public delegate void ClientConnectedEventHandler();

	[Signal]
	public delegate void ClientConnectionFailedEventHandler();

	[Signal]
	public delegate void ServerDisconnectedEventHandler();

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
	}

	public Error Join(string address, int port = 7777)
	{
		var peer = new ENetMultiplayerPeer();
		var err = peer.CreateClient(address, port);

		if (err != Error.Ok)
		{
			GD.PrintErr($"Failed to create client for {address}:{port}: " + err);
			return err;
		}

		Multiplayer.MultiplayerPeer = peer;
		GD.Print($"Joining {address}:{port}");

		return Error.Ok;
	}

	private void OnPeerConnected(long id)
	{
		GD.Print($"Peer connected: {id}");
	}

	private void OnPeerDisconnected(long id)
	{
		GD.Print($"Peer disconnected: {id}");
		GameManager.Instance.PeerDisconnected((int)id);
	}

	private void OnConnectedToServer()
	{
		GD.Print("Connected to server");
		EmitSignal(SignalName.ClientConnected);
		GameManager.Instance.RpcId(1, nameof(GameManager.RequestInitialJoinState));
	}

	private void OnConnectionFailed()
	{
		GD.PrintErr("Connection failed");
		Multiplayer.MultiplayerPeer = null;
		EmitSignal(SignalName.ClientConnectionFailed);
	}

	private void OnServerDisconnected()
	{
		GD.PrintErr("Server disconnected");
		Multiplayer.MultiplayerPeer = null;
		EmitSignal(SignalName.ServerDisconnected);
	}

	public bool IsClientFullyConnected =>
		Multiplayer.MultiplayerPeer != null &&
		Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected;
}