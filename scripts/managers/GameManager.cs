using Godot;

public partial class GameManager : Node
{
    public static GameManager Instance;

    [Signal] public delegate void LocalPlayerChangedEventHandler(Player p);

    public DayNightCycle DayNightCycle { get; private set; }
    public WeatherManager WeatherManager { get; private set; }

    public Player LocalPlayer { get; private set; }

    public override void _Ready()
    {
        Instance = this;

        DayNightCycle = GetNode<DayNightCycle>("/root/Game/World/DayNightCycle");
        WeatherManager = GetNode<WeatherManager>("/root/Game/World/WeatherManager");

        GD.Print("GameManager initialized.");
    }

    public void SetLocalPlayer(Player p)
    {
        LocalPlayer = p;
        EmitSignal(SignalName.LocalPlayerChanged, p);
    }
}
