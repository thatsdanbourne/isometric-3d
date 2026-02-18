using Godot;
using System.Collections.Generic;

public class AudioRegistry
{
	public readonly Dictionary<string, AudioStream> Sfx = new();
	public readonly Dictionary<string, AudioStream[]> SfxVariants = new();
	public readonly Dictionary<string, AudioStream> Ambiance = new();
	public readonly List<AudioStream> Music = new();

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
			GD.Load<AudioStream>("res://assets/audio/hit/wood/wood_5.wav"),
			GD.Load<AudioStream>("res://assets/audio/hit/wood/wood_6.wav")
		];

		// Stone hit sounds
		SfxVariants["hit_stone"] =
		[
			GD.Load<AudioStream>("res://assets/audio/hit/stone/stone_1.wav"),
			GD.Load<AudioStream>("res://assets/audio/hit/stone/stone_2.wav"),
			GD.Load<AudioStream>("res://assets/audio/hit/stone/stone_3.wav"),
			GD.Load<AudioStream>("res://assets/audio/hit/stone/stone_4.wav"),
			GD.Load<AudioStream>("res://assets/audio/hit/stone/stone_5.wav"),
			GD.Load<AudioStream>("res://assets/audio/hit/stone/stone_6.wav"),
			GD.Load<AudioStream>("res://assets/audio/hit/stone/stone_7.wav")
		];

		Sfx["hit_fail"] = GD.Load<AudioStream>("res://assets/audio/hit/hit_fail.wav");

		// Swing sounds
		SfxVariants["swing_fist"] =
		[
			GD.Load<AudioStream>("res://assets/audio/tools/fist/fist_1.wav"),
			GD.Load<AudioStream>("res://assets/audio/tools/fist/fist_2.wav"),
			GD.Load<AudioStream>("res://assets/audio/tools/fist/fist_3.wav"),
			GD.Load<AudioStream>("res://assets/audio/tools/fist/fist_4.wav"),
			GD.Load<AudioStream>("res://assets/audio/tools/fist/fist_5.wav"),
			GD.Load<AudioStream>("res://assets/audio/tools/fist/fist_6.wav")
			// GD.Load<AudioStream>("res://assets/audio/tools/fist/fist_7.wav"),
		];

		SfxVariants["swing_sword"] =
		[
			GD.Load<AudioStream>("res://assets/audio/tools/sword/sword_1.wav"),
			GD.Load<AudioStream>("res://assets/audio/tools/sword/sword_2.wav"),
			GD.Load<AudioStream>("res://assets/audio/tools/sword/sword_3.wav"),
			GD.Load<AudioStream>("res://assets/audio/tools/sword/sword_4.wav")
		];
	}

	private void LoadAmbiance()
	{
		Ambiance["forest_day"] = GD.Load<AudioStream>("res://assets/audio/ambience/forest_day.wav");
		Ambiance["forest_night"] = GD.Load<AudioStream>("res://assets/audio/ambience/forest_night.wav");
		Ambiance["snowstorm"] = GD.Load<AudioStream>("res://assets/audio/ambience/snowstorm.wav");
		Ambiance["rain"] = GD.Load<AudioStream>("res://assets/audio/ambience/rain.wav");
		Ambiance["desert"] = GD.Load<AudioStream>("res://assets/audio/ambience/desert.wav");
	}

	private void LoadMusic()
	{
		Music.Add(GD.Load<AudioStream>("res://assets/audio/music/relativity.mp3"));
		Music.Add(GD.Load<AudioStream>("res://assets/audio/music/explorers.mp3"));
	}

	public AudioStream GetSfx(string key)
	{
		return Sfx.GetValueOrDefault(key);
	}

	public AudioStream GetAmbiance(string key)
	{
		return Ambiance.GetValueOrDefault(key);
	}
}