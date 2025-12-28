using System.Collections.Generic;
using Godot;

public static class BiomeWeather
{
    public static readonly Dictionary<string, (WeatherType type, float chance)[]> Rules = new()
    {
        { "Plains", new [] {
            (WeatherType.Clear, 0.6f),
            (WeatherType.Rain, 0.4f)
        }},
        { "Forest", new [] {
            (WeatherType.Clear, 0.5f),
             (WeatherType.Rain, 0.5f),
        }},
        { "Desert", new [] {
            (WeatherType.Clear, 0.9f),
             (WeatherType.Rain, 0.1f)
        }},
        { "Tundra", new [] {
            (WeatherType.Clear, 0.3f),
             (WeatherType.Snow, 0.7f)
        }},
        { "Taiga", new [] {
            (WeatherType.Clear, 0.4f),
             (WeatherType.Snow, 0.6f)
        }},
    };
}
