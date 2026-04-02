using Godot;
using System;

public partial class DebugLaunchConfig : Node
{
	public DebugSessionMode SessionMode { get; private set; } = DebugSessionMode.None;
	public WorldLoadMode WorldMode { get; private set; } = WorldLoadMode.Random;

	public int Seed { get; private set; }
	public string SaveName { get; private set; } = "";

	public string Address { get; private set; } = "127.0.0.1";
	public int Port { get; private set; } = 7777;

	public override void _Ready()
	{
		foreach (var arg in OS.GetCmdlineArgs())
			if (arg.StartsWith("--debug-session="))
			{
				var value = arg.Split("=")[1].ToLower();
				SessionMode = value switch
				{
					"menu" => DebugSessionMode.Menu,
					"single" => DebugSessionMode.Single,
					"host" => DebugSessionMode.Host,
					"client" => DebugSessionMode.Client,
					_ => DebugSessionMode.None
				};
			}
			else if (arg.StartsWith("--world="))
			{
				var value = arg.Split("=")[1].ToLower();
				WorldMode = value switch
				{
					"random" => WorldLoadMode.Random,
					"seed" => WorldLoadMode.Seed,
					"save" => WorldLoadMode.Save,
					_ => WorldLoadMode.Random
				};
			}
			else if (arg.StartsWith("--seed="))
			{
				int.TryParse(arg.Split("=")[1], out var seed);
				Seed = seed;
			}
			else if (arg.StartsWith("--save="))
			{
				SaveName = arg.Split("=")[1];
			}
			else if (arg.StartsWith("--address="))
			{
				Address = arg.Split("=")[1];
			}
			else if (arg.StartsWith("--port="))
			{
				int.TryParse(arg.Split("=")[1], out var port);
				Port = port;
			}
	}
}