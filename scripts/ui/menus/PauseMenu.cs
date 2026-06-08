using Godot;

public partial class PauseMenu : Control
{
	[Signal]
	public delegate void OnQuitTitleRequestedEventHandler();

	private Button _resume;
	private Button _settings;
	private Button _quitTitle;
	private Button _quitDesktop;

	private bool IsOpen { get; set; }


	public override void _Ready()
	{
		Visible = false;

		_resume = GetNode<Button>("VBoxContainer/MarginContainer/VBoxContainer/Resume");
		_settings = GetNode<Button>("VBoxContainer/MarginContainer/VBoxContainer/Settings");
		_quitTitle = GetNode<Button>("VBoxContainer/MarginContainer/VBoxContainer/QuitTitle");
		_quitDesktop = GetNode<Button>("VBoxContainer/MarginContainer/VBoxContainer/QuitDesktop");

		_resume.Pressed += OnResume;
		_settings.Pressed += OnSettings;
		_quitTitle.Pressed += OnQuitTitle;
		_quitDesktop.Pressed += OnQuitDesktop;
	}

	public void Open()
	{
		MenuManager.Instance.Push(this);

		GetTree().Paused = true;
		IsOpen = true;
	}

	public void Close()
	{
		MenuManager.Instance.ClearStack();

		GetTree().Paused = false;
		Visible = false;
		IsOpen = false;
	}

	private void OnResume()
	{
		Close();
	}

	private void OnSettings()
	{
		var menu = MenuManager.Instance.GetNode<SettingsMenu>("SettingsMenu");
		MenuManager.Instance.Push(menu);
	}

	private void OnQuitTitle()
	{
		EmitSignal(SignalName.OnQuitTitleRequested);
	}

	private void OnQuitDesktop()
	{
		GetTree().Quit();
	}
}