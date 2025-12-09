using Godot;
using System.Collections.Generic;

public partial class AudioRegistry
{
    public Dictionary<string, AudioStream> Sfx = new();
    public Dictionary<string, AudioStream[]> SfxVariants = new();
    public Dictionary<string, AudioStream> Ambiance = new();
    public List<AudioStream> Music = new();

    public AudioRegistry()
    {
        LoadSfx();
        LoadAmbiance();
        LoadMusic();
    }

    private void LoadSfx()
    {
        Sfx["pickup_pop"] = GD.Load<AudioStream>("res://assets/audio/sfx_pop.wav");

        // Wood hit sounds
        SfxVariants["hit_wood"] =
        [
            GD.Load<AudioStream>("res://assets/audio/hit/wood/wood_1.wav"),
            GD.Load<AudioStream>("res://assets/audio/hit/wood/wood_2.wav"),
            GD.Load<AudioStream>("res://assets/audio/hit/wood/wood_3.wav"),
            GD.Load<AudioStream>("res://assets/audio/hit/wood/wood_4.wav"),
        ];

        // Stone hit sounds
        SfxVariants["hit_stone"] =
        [
            GD.Load<AudioStream>("res://assets/audio/hit/stone/mine_1.wav"),
            GD.Load<AudioStream>("res://assets/audio/hit/stone/mine_2.wav"),
            GD.Load<AudioStream>("res://assets/audio/hit/stone/mine_3.wav"),
            GD.Load<AudioStream>("res://assets/audio/hit/stone/mine_4.wav"),
            GD.Load<AudioStream>("res://assets/audio/hit/stone/mine_5.wav"),
        ];

        // Swing sounds
        Sfx["fist_1"] = GD.Load<AudioStream>("res://assets/audio/tools/fist/fist_1.wav");
        Sfx["sword_1"] = GD.Load<AudioStream>("res://assets/audio/tools/sword/sword_1.mp3");

    }

    private void LoadAmbiance()
    {
        Ambiance["forest_day"] = GD.Load<AudioStream>("res://assets/audio/ambience/forest_day.wav");
        Ambiance["forest_night"] = GD.Load<AudioStream>("res://assets/audio/ambience/forest_night.mp3");
        Ambiance["cold_wind"] = GD.Load<AudioStream>("res://assets/audio/ambience/cold_wind.wav");
        Ambiance["rain"] = GD.Load<AudioStream>("res://assets/audio/ambience/rain.wav");
    }

    private void LoadMusic()
    {
        Music.Add(GD.Load<AudioStream>("res://assets/audio/music/relativity.mp3"));
        Music.Add(GD.Load<AudioStream>("res://assets/audio/music/explorers.mp3"));
    }

    public AudioStream GetSfx(string key) => Sfx.TryGetValue(key, out var stream) ? stream : null;
    public AudioStream GetAmbiance(string key) => Ambiance.TryGetValue(key, out var stream) ? stream : null;
}
