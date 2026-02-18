using Godot;

public partial class PauseMenu : Control
{
	private Button _resume;
	private Button _settings;
	private Button _quitDesktop;

	private bool IsOpen { get; set; }


	public override void _Ready()
	{
		Visible = false;

		_resume = GetNode<Button>("VBoxContainer/MarginContainer/VBoxContainer/Resume");
		_settings = GetNode<Button>("VBoxContainer/MarginContainer/VBoxContainer/Settings");
		_quitDesktop = GetNode<Button>("VBoxContainer/MarginContainer/VBoxContainer/QuitDesktop");

		_resume.Pressed += OnResume;
		_settings.Pressed += OnSettings;
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

	private void OnQuitDesktop()
	{
		GetTree().Quit();
	}
}