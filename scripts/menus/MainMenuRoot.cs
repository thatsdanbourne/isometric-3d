using Godot;

public partial class MainMenuRoot : Control
{
	[Signal]
	public delegate void SingleplayerRequestedEventHandler();

	[Signal]
	public delegate void MultiplayerRequestedEventHandler();

	private MainMenu _mainMenu;
	private SettingsMenu _settingsMenu;

	public override void _Ready()
	{
		_mainMenu = GetNode<MainMenu>("MenuManager/MainMenu");
		_settingsMenu = GetNode<SettingsMenu>("MenuManager/SettingsMenu");

		MenuManager.Instance.Push(_mainMenu);

		_mainMenu.SingleplayerPressed += () => EmitSignal(SignalName.SingleplayerRequested);
		_mainMenu.MultiplayerPressed += () => EmitSignal(SignalName.MultiplayerRequested);
		_mainMenu.SettingsPressed += () => MenuManager.Instance.Push(_settingsMenu);
		_mainMenu.QuitPressed += () => GetTree().Quit();
	}
}