using Godot;
using System;

public partial class SettingsVideo : Control
{
	SettingsManager S => SettingsManager.Instance;

	private CheckBox fullscreen;
	private CheckBox borderless;
	private CheckBox vsync;
	private CheckBox ssao;

	private Button applyButton;
	private Button backButton;

	public override void _Ready()
    {
		Visible = false;
		
        fullscreen = GetNode<CheckBox>("VBoxContainer/Fullscreen");
        borderless = GetNode<CheckBox>("VBoxContainer/Borderless");
        vsync = GetNode<CheckBox>("VBoxContainer/VSync");
        ssao = GetNode<CheckBox>("VBoxContainer/SSAO");

		applyButton = GetNode<Button>("VBoxContainer/Apply");
		backButton = GetNode<Button>("VBoxContainer/Back");

		applyButton.Pressed += OnApply;
		backButton.Pressed += OnBack;

		LoadSettings();
    }

	private void LoadSettings()
    {
        fullscreen.ButtonPressed = S.Fullscreen;
		borderless.ButtonPressed = S.Borderless;
		vsync.ButtonPressed = S.VSync;
		ssao.ButtonPressed = S.SSAOEnabled;
    }

	private void OnApply()
    {
        S.Fullscreen = fullscreen.ButtonPressed;
		S.Borderless = borderless.ButtonPressed;
		S.VSync = vsync.ButtonPressed;
		S.SSAOEnabled = ssao.ButtonPressed;

		S.ApplyAll();
    }

	private void OnBack()
	{
		MenuManager.Instance.Pop();
		LoadSettings();
	}
}
