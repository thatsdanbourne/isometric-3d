using System.Collections.Generic;

public static class BiomeWeather
{
	public static readonly Dictionary<string, (WeatherType type, float chance)[]> Rules = new()
	{
		{
			"Plains", [
				(WeatherType.Clear, 0.8f),
				(WeatherType.Rain, 0.2f)
			]
		},
		{
			"Forest", [
				(WeatherType.Clear, 0.7f),
				(WeatherType.Rain, 0.3f)
			]
		},
		{
			"Desert", [
				(WeatherType.Clear, 0.9f),
				(WeatherType.Rain, 0.1f)
			]
		},
		{
			"Tundra", [
				(WeatherType.Clear, 0.8f),
				(WeatherType.Snow, 0.2f)
			]
		},
		{
			"Taiga", [
				(WeatherType.Clear, 0.7f),
				(WeatherType.Snow, 0.3f)
			]
		}
	};
}