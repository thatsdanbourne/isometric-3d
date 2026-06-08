using Godot;

public partial class SettingsMenu : Control
{
    public override void _Ready()
    {
        Visible = false;

        GetNode<Button>("VBoxContainer/MarginContainer/VBoxContainer/Video").Pressed += () =>
        {
            MenuManager.Instance.Push(GetNode<Control>("../SettingsVideo"));
        };

        // GetNode<Button>("VBoxContainer/MarginContainer/VBoxContainer/Audio").Pressed += () =>
        // {
        //     MenuManager.Instance.Push(GetNode<Control>("../SettingsAudio"));
        // };

        // GetNode<Button>("VBoxContainer/MarginContainer/VBoxContainer/Gameplay").Pressed += () =>
        // {
        //     MenuManager.Instance.Push(GetNode<Control>("../SettingsGameplay"));
        // };

        GetNode<Button>("VBoxContainer/MarginContainer/VBoxContainer/Back").Pressed += () =>
        {
            MenuManager.Instance.Pop();
        };
    }
}
