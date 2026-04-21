using Godot;

public partial class WorldSync
{
	public void SendWeatherState(WeatherState state)
	{
		if (!_world.Multiplayer.IsServer())
			return;

		Rpc(nameof(ReceiveWeatherState),
			(int)state.Weather,
			state.Intensity,
			state.TransitionStartWorldTime,
			state.TransitionDuration
		);
	}

	public void SendWeatherStateToPeer(int peerId, WeatherState state)
	{
		if (!_world.Multiplayer.IsServer())
			return;

		RpcId(
			peerId,
			nameof(ReceiveWeatherState),
			(int)state.Weather,
			state.Intensity,
			state.TransitionStartWorldTime,
			state.TransitionDuration
		);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	public void ReceiveWeatherState(
		int globalWeather,
		float intensity,
		double transitionStartWorldTime,
		double transitionDuration)
	{
		var state = new WeatherState
		{
			Weather = (GlobalWeatherType)globalWeather,
			Intensity = intensity,
			TransitionStartWorldTime = transitionStartWorldTime,
			TransitionDuration = transitionDuration
		};

		_world.WeatherManager.ApplyWeatherState(state);
	}
}