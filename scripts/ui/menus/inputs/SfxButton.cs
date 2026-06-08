using Godot;
using System;

public partial class SfxButton : Button
{
	[Export] public ButtonType Type = ButtonType.Default;

	public override void _Ready()
	{
		MouseEntered += OnMouseEntered;
		Pressed += OnPressed;
	}

	private void OnMouseEntered()
	{
		PlayUiSfx(UiSfx.Hover);
	}

	private void OnPressed()
	{
		var sfx = Type switch
		{
			ButtonType.Back => UiSfx.Back,
			_ => UiSfx.Click
		};

		PlayUiSfx(sfx);
	}

	private void PlayUiSfx(string key)
	{
		AudioManager.Instance.PlaySfx(key, 0.1f);
	}
}

public enum ButtonType
{
	Default,
	Back
}

public static class UiSfx
{
	public const string Hover = "ui_hover";
	public const string Click = "ui_click";
	public const string Back = "ui_back";
	public const string ToggleOn = "ui_toggle_on";
	public const string ToggleOff = "ui_toggle_off";
}