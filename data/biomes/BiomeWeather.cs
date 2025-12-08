using System.Collections.Generic;
using Godot;

public static class BiomeWeather
{
    public static readonly Dictionary<string, (WeatherManager.WeatherType type, float chance)[]> Rules = new()
    {
        { "Plains", new [] {
            (WeatherManager.WeatherType.Clear, 0.6f),
            (WeatherManager.WeatherType.Rain, 0.4f)
        }},
        { "Forest", new [] {
            (WeatherManager.WeatherType.Clear, 0.5f),
             (WeatherManager.WeatherType.Rain, 0.5f),
        }},
        { "Desert", new [] {
            (WeatherManager.WeatherType.Clear, 0.9f),
             (WeatherManager.WeatherType.Rain, 0.1f)
        }},
        { "Tundra", new [] {
            (WeatherManager.WeatherType.Clear, 0.3f),
             (WeatherManager.WeatherType.Snow, 0.7f)
        }},
        { "Taiga", new [] {
            (WeatherManager.WeatherType.Clear, 0.4f),
             (WeatherManager.WeatherType.Snow, 0.6f)
        }},
    };
}
