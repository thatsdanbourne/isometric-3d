using Godot;

public partial class MainMenu : Control
{
	[Signal]
	public delegate void SingleplayerPressedEventHandler();

	[Signal]
	public delegate void MultiplayerPressedEventHandler();

	[Signal]
	public delegate void SettingsPressedEventHandler();

	[Signal]
	public delegate void QuitPressedEventHandler();

	private Button _singleplayerButton;
	private Button _multiplayerButton;
	private Button _settingsButton;
	private Button _quitButton;

	public override void _Ready()
	{
		Visible = false;

		var buttonContainer = GetNode<VBoxContainer>("VBoxContainer/MarginContainer/VBoxContainer");
		_singleplayerButton = buttonContainer.GetNode<Button>("SingleplayerButton");
		_multiplayerButton = buttonContainer.GetNode<Button>("MultiplayerButton");
		_settingsButton = buttonContainer.GetNode<Button>("SettingsButton");
		_quitButton = buttonContainer.GetNode<Button>("QuitButton");

		_singleplayerButton.Pressed += OnSingleplayer;
		_multiplayerButton.Pressed += OnMultiplayer;
		_settingsButton.Pressed += OnSettings;
		_quitButton.Pressed += OnQuitDesktop;
	}

	private void OnSingleplayer()
	{
		EmitSignal(nameof(SingleplayerPressed));
	}

	private void OnMultiplayer()
	{
		EmitSignal(nameof(MultiplayerPressed));
	}

	private void OnSettings()
	{
		EmitSignal(nameof(SettingsPressed));
	}

	private void OnQuitDesktop()
	{
		EmitSignal(nameof(QuitPressed));
	}
}