using Godot;

public partial class SettingsManager : Node
{
    public static SettingsManager Instance;
    private static string ConfigPath = "user://settings.cfg";

    public bool Fullscreen = false;
    public bool Borderless = false;
    public bool VSync = true;
    public bool SSAOEnabled = true;

    public override void _Ready()
    {
        Instance = this;
        LoadSettings();
        ApplyAll();
    }

    public void SaveSettings()
    {
        var cfg = new ConfigFile();
        cfg.SetValue("video", "fullscreen", Fullscreen);
        cfg.SetValue("video", "borderless", Borderless);
        cfg.SetValue("video", "vsync", VSync);
        cfg.SetValue("video", "ssao", SSAOEnabled);

        cfg.Save(ConfigPath);
    }

    public void LoadSettings()
    {
        var cfg = new ConfigFile();
        Error err = cfg.Load(ConfigPath);

        if (err != Error.Ok)
            return;
        
        Fullscreen = (bool)cfg.GetValue("video", "fullscreen", Fullscreen);
        Borderless = (bool)cfg.GetValue("video", "borderless", Borderless);
        VSync = (bool)cfg.GetValue("video", "vsync", VSync);
        SSAOEnabled = (bool)cfg.GetValue("video", "ssao", SSAOEnabled);
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

       environment.SsaoEnabled = SSAOEnabled;
    }
}
