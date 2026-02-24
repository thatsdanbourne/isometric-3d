using Godot;

public partial class SettingsVideo : Control
{
	private SettingsManager S => SettingsManager.Instance;

	private CheckBox _fullscreen;
	private CheckBox _borderless;
	private CheckBox _vsync;
	private CheckBox _ssao;

	private Button _applyButton;
	private Button _backButton;

	private VideoSnapshot _baseline;

	private readonly struct VideoSnapshot(bool fullscreen, bool borderless, bool vsync, bool ssao)
	{
		private readonly bool _fullscreen = fullscreen;
		private readonly bool _borderless = borderless;
		private readonly bool _vSync = vsync;
		private readonly bool _ssao = ssao;

		public bool Equals(VideoSnapshot other)
		{
			return _fullscreen == other._fullscreen && _borderless == other._borderless && _vSync == other._vSync &&
			       _ssao == other._ssao;
		}
	}

	public override void _Ready()
	{
		Visible = false;

		_fullscreen = GetNode<CheckBox>("VBoxContainer/MarginContainer/VBoxContainer/Fullscreen");
		_borderless = GetNode<CheckBox>("VBoxContainer/MarginContainer/VBoxContainer/Borderless");
		_vsync = GetNode<CheckBox>("VBoxContainer/MarginContainer/VBoxContainer/VSync");
		_ssao = GetNode<CheckBox>("VBoxContainer/MarginContainer/VBoxContainer/SSAO");

		_applyButton = GetNode<Button>("VBoxContainer/HBoxContainer/Apply");
		_backButton = GetNode<Button>("VBoxContainer/HBoxContainer/Back");

		_applyButton.Pressed += OnApply;
		_backButton.Pressed += OnBack;

		_fullscreen.Toggled += _ => UpdateApplyEnabled();
		_borderless.Toggled += _ => UpdateApplyEnabled();
		_vsync.Toggled += _ => UpdateApplyEnabled();
		_ssao.Toggled += _ => UpdateApplyEnabled();

		LoadSettings();
	}

	private void LoadSettings()
	{
		_fullscreen.ButtonPressed = S.Fullscreen;
		_borderless.ButtonPressed = S.Borderless;
		_vsync.ButtonPressed = S.VSync;
		_ssao.ButtonPressed = S.SsaoEnabled;

		_baseline = SnapshotFromSettings();
		UpdateApplyEnabled();
	}

	private void OnApply()
	{
		S.Fullscreen = _fullscreen.ButtonPressed;
		S.Borderless = _borderless.ButtonPressed;
		S.VSync = _vsync.ButtonPressed;
		S.SsaoEnabled = _ssao.ButtonPressed;

		S.ApplyAll();

		_baseline = SnapshotFromUI();
		UpdateApplyEnabled();
	}

	private void OnBack()
	{
		MenuManager.Instance.Pop();
		LoadSettings();
	}

	private void UpdateApplyEnabled()
	{
		var current = SnapshotFromUI();
		_applyButton.Disabled = _baseline.Equals(current);
	}

	private VideoSnapshot SnapshotFromUI()
	{
		return new VideoSnapshot(_fullscreen.ButtonPressed, _borderless.ButtonPressed, _vsync.ButtonPressed,
			_ssao.ButtonPressed);
	}

	private VideoSnapshot SnapshotFromSettings()
	{
		return new VideoSnapshot(S.Fullscreen, S.Borderless, S.VSync, S.SsaoEnabled);
	}
}