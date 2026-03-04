using System.Collections.Generic;

public static class BiomeWeather
{
	public static readonly Dictionary<BiomeId, (WeatherType type, float chance)[]> Rules = new()
	{
		{
			BiomeId.Plains, [
				(WeatherType.Clear, 0.8f),
				(WeatherType.Rain, 0.2f)
			]
		},
		{
			BiomeId.Forest, [
				(WeatherType.Clear, 0.7f),
				(WeatherType.Rain, 0.3f)
			]
		},
		{
			BiomeId.Desert, [
				(WeatherType.Clear, 0.9f),
				(WeatherType.Rain, 0.1f)
			]
		},
		{
			BiomeId.Tundra, [
				(WeatherType.Clear, 0.8f),
				(WeatherType.Snow, 0.2f)
			]
		},
		{
			BiomeId.Taiga, [
				(WeatherType.Clear, 0.7f),
				(WeatherType.Snow, 0.3f)
			]
		}
	};
}