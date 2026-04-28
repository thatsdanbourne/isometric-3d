using Godot;
using System;

public partial class MultiplayerMenu : Control
{
	[Signal]
	public delegate void ConnectPressedEventHandler(string ip, int port);

	private LineEdit _serverAddress;
	private LineEdit _serverPort;
	private Button _backButton;
	private Button _connectButton;
	private Label _statusLabel;

	public override void _Ready()
	{
		Visible = false;

		_serverAddress = GetNode<LineEdit>("VBoxContainer/MarginContainer/VBoxContainer2/HBoxContainer/ServerAddress");
		_serverPort = GetNode<LineEdit>("VBoxContainer/MarginContainer/VBoxContainer2/HBoxContainer/Port");
		_statusLabel = GetNode<Label>("VBoxContainer/MarginContainer/VBoxContainer2/StatusLabel");
		_backButton = GetNode<Button>("VBoxContainer/HBoxContainer2/Back");
		_connectButton = GetNode<Button>("VBoxContainer/HBoxContainer2/Connect");

		_backButton.Pressed += () => MenuManager.Instance.Pop();
		_connectButton.Pressed += OnConnectPressed;
	}

	private void OnConnectPressed()
	{
		var address = _serverAddress.Text.Trim();

		if (!int.TryParse(_serverPort.Text.Trim(), out var port))
		{
			SetStatus("Invalid port");
			return;
		}

		EmitSignal(SignalName.ConnectPressed, address, port);
	}

	public void SetConnectButtonEnabled(bool enabled)
	{
		_connectButton.Disabled = !enabled;
	}

	public void SetStatus(string text)
	{
		_statusLabel.Text = text;
	}
}