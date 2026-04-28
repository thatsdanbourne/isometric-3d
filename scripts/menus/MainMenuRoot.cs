using Godot;

public partial class MainMenuRoot : Control
{
	[Signal]
	public delegate void SingleplayerRequestedEventHandler();

	[Signal]
	public delegate void MultiplayerRequestedEventHandler();

	private MainMenu _mainMenu;
	private MultiplayerMenu _multiplayerMenu;
	private SettingsMenu _settingsMenu;

	public override void _Ready()
	{
		_mainMenu = GetNode<MainMenu>("MenuManager/MainMenu");
		_multiplayerMenu = GetNode<MultiplayerMenu>("MenuManager/MultiplayerMenu");
		_settingsMenu = GetNode<SettingsMenu>("MenuManager/SettingsMenu");

		MenuManager.Instance.Push(_mainMenu);

		_mainMenu.SingleplayerPressed += () => EmitSignal(SignalName.SingleplayerRequested);
		_mainMenu.MultiplayerPressed += () => MenuManager.Instance.Push(_multiplayerMenu);
		_mainMenu.SettingsPressed += () => MenuManager.Instance.Push(_settingsMenu);
		_mainMenu.QuitPressed += () => GetTree().Quit();
	}
}