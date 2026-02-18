using Godot;

public partial class SettingsManager : Node
{
	public static SettingsManager Instance;
	private static readonly string ConfigPath = "user://settings.cfg";

	public bool Fullscreen;
	public bool Borderless;
	public bool VSync = true;
	public bool SsaoEnabled = true;

	public override void _Ready()
	{
		Instance = this;
		LoadSettings();
		ApplyAll();
	}

	private void SaveSettings()
	{
		var cfg = new ConfigFile();
		cfg.SetValue("video", "fullscreen", Fullscreen);
		cfg.SetValue("video", "borderless", Borderless);
		cfg.SetValue("video", "vsync", VSync);
		cfg.SetValue("video", "ssao", SsaoEnabled);

		cfg.Save(ConfigPath);
	}

	private void LoadSettings()
	{
		var cfg = new ConfigFile();
		var err = cfg.Load(ConfigPath);

		if (err != Error.Ok)
			return;

		Fullscreen = (bool)cfg.GetValue("video", "fullscreen", Fullscreen);
		Borderless = (bool)cfg.GetValue("video", "borderless", Borderless);
		VSync = (bool)cfg.GetValue("video", "vsync", VSync);
		SsaoEnabled = (bool)cfg.GetValue("video", "ssao", SsaoEnabled);
	}

	public void ApplyAll()
	{
		ApplyDisplay();
		ApplyRendering();
		SaveSettings();
	}

	private void ApplyDisplay()
	{
		DisplayServer.WindowSetMode(Fullscreen
			? DisplayServer.WindowMode.Fullscreen
			: DisplayServer.WindowMode.Windowed);

		DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, Borderless);

		DisplayServer.WindowSetVsyncMode(VSync
			? DisplayServer.VSyncMode.Enabled
			: DisplayServer.VSyncMode.Disabled);
	}

	private void ApplyRendering()
	{
		var environment = GetNode<WorldEnvironment>("/root/Game/World/WorldEnvironment").Environment;
		if (environment == null) return;

		environment.SsaoEnabled = SsaoEnabled;
	}
}