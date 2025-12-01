using Godot;

public partial class PauseMenu : Control
{
	private Button resume;
	private Button settings;
	private Button quitDesktop;

	private BlurOverlay blur;

	public bool IsOpen { get; private set; } = false;

	public override void _Ready()
	{
		Visible = false;

		resume = GetNode<Button>("VBoxContainer/Resume");
		settings = GetNode<Button>("VBoxContainer/Settings");
		quitDesktop = GetNode<Button>("VBoxContainer/QuitDesktop");

		blur = GetNode<BlurOverlay>("../BlurOverlay");

		resume.Pressed += OnResume;
		settings.Pressed += OnSettings;
		quitDesktop.Pressed += OnQuitDesktop;
	}

	public void Open()
    {
        MenuManager.Instance.ClearStack();
        MenuManager.Instance.Push(this);

        GetTree().Paused = true;
        blur.FadeIn();

        IsOpen = true;
    }

	public void Close()
    {
        MenuManager.Instance.ClearStack();

        blur.FadeOut();
        GetTree().Paused = false;

        Visible = false;
        IsOpen = false;
    }

	private void OnResume() => Close();

	private void OnSettings()
    {
        var menu = GetNode<Control>("../SettingsMenu");
        MenuManager.Instance.Push(menu);
    }

	private void OnQuitDesktop() => GetTree().Quit();
}
