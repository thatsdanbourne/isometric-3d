using Godot;

public partial class SettingsMenu : Control
{
    public override void _Ready()
    {
        Visible = false;

        GetNode<Button>("VBoxContainer/Video").Pressed += () =>
        {
            MenuManager.Instance.Push(GetNode<Control>("../SettingsVideo"));
        };

        // GetNode<Button>("VBoxContainer/Audio").Pressed += () =>
        // {
        //     MenuManager.Instance.Push(GetNode<Control>("../SettingsAudio"));
        // };

        // GetNode<Button>("VBoxContainer/Gameplay").Pressed += () =>
        // {
        //     MenuManager.Instance.Push(GetNode<Control>("../SettingsGameplay"));
        // };

        GetNode<Button>("VBoxContainer/Back").Pressed += () =>
        {
            MenuManager.Instance.Pop();
        };
    }
}
