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

		// ui
		Sfx["ui_hover"] = GD.Load<AudioStream>("res://assets/audio/ui/ui_hover.wav");
		Sfx["ui_click"] = GD.Load<AudioStream>("res://assets/audio/ui/ui_click.wav");
		Sfx["ui_back"] = GD.Load<AudioStream>("res://assets/audio/ui/ui_back.wav");


		// inventory
		SfxVariants["ui_inventory_open"] =
		[
			GD.Load<AudioStream>("res://assets/audio/ui/inventory/toggle/Open Inventory Bag A.wav"),
			GD.Load<AudioStream>("res://assets/audio/ui/inventory/toggle/Open Inventory Bag B.wav"),
			GD.Load<AudioStream>("res://assets/audio/ui/inventory/toggle/Open Inventory Bag C.wav"),
			GD.Load<AudioStream>("res://assets/audio/ui/inventory/toggle/Open Inventory Bag D.wav"),
			GD.Load<AudioStream>("res://assets/audio/ui/inventory/toggle/Open Inventory Bag E.wav"),
			GD.Load<AudioStream>("res://assets/audio/ui/inventory/toggle/Open Inventory Bag F.wav")
		];

		SfxVariants["ui_inventory_close"] =
		[
			GD.Load<AudioStream>("res://assets/audio/ui/inventory/toggle/Close Inventory Bag A.wav"),
			GD.Load<AudioStream>("res://assets/audio/ui/inventory/toggle/Close Inventory Bag B.wav"),
			GD.Load<AudioStream>("res://assets/audio/ui/inventory/toggle/Close Inventory Bag C.wav"),
			GD.Load<AudioStream>("res://assets/audio/ui/inventory/toggle/Close Inventory Bag D.wav")
		];

		Sfx["ui_inventory_generic_pickup"] =
			GD.Load<AudioStream>("res://assets/audio/ui/inventory/generic/generic_pickup.wav");
		Sfx["ui_inventory_generic_drop"] =
			GD.Load<AudioStream>("res://assets/audio/ui/inventory/generic/generic_drop.wav");

		Sfx["ui_inventory_wood_pickup"] =
			GD.Load<AudioStream>("res://assets/audio/ui/inventory/wood/wood_pickup.wav");
		Sfx["ui_inventory_wood_drop"] = GD.Load<AudioStream>("res://assets/audio/ui/inventory/wood/wood_drop.wav");

		Sfx["ui_inventory_stone_pickup"] =
			GD.Load<AudioStream>("res://assets/audio/ui/inventory/stone/stone_pickup.wav");
		Sfx["ui_inventory_stone_drop"] = GD.Load<AudioStream>("res://assets/audio/ui/inventory/stone/stone_drop.wav");

		Sfx["ui_inventory_sword_pickup"] =
			GD.Load<AudioStream>("res://assets/audio/ui/inventory/sword/sword_pickup.wav");
		Sfx["ui_inventory_sword_drop"] = GD.Load<AudioStream>("res://assets/audio/ui/inventory/sword/sword_drop.wav");

		Sfx["ui_inventory_ore_pickup"] =
			GD.Load<AudioStream>("res://assets/audio/ui/inventory/ore/ore_pickup.wav");
		Sfx["ui_inventory_ore_drop"] = GD.Load<AudioStream>("res://assets/audio/ui/inventory/ore/ore_drop.wav");

		Sfx["ui_inventory_ingot_pickup"] =
			GD.Load<AudioStream>("res://assets/audio/ui/inventory/ingot/ingot_pickup.wav");
		Sfx["ui_inventory_ingot_drop"] = GD.Load<AudioStream>("res://assets/audio/ui/inventory/ingot/ingot_drop.wav");


		// crafting

		Sfx["ui_kiln_place"] = GD.Load<AudioStream>("res://assets/audio/ui/crafting/kiln_place.wav");
		Sfx["ui_craft_generic"] = GD.Load<AudioStream>("res://assets/audio/ui/crafting/craft_generic.wav");

		// hit sounds
		SfxVariants["hit_wood"] =
		[
			GD.Load<AudioStream>("res://assets/audio/hit/wood/wood_1.wav"),
			GD.Load<AudioStream>("res://assets/audio/hit/wood/wood_2.wav"),
			GD.Load<AudioStream>("res://assets/audio/hit/wood/wood_3.wav"),
			GD.Load<AudioStream>("res://assets/audio/hit/wood/wood_4.wav"),
			GD.Load<AudioStream>("res://assets/audio/hit/wood/wood_5.wav"),
			GD.Load<AudioStream>("res://assets/audio/hit/wood/wood_6.wav")
		];

		SfxVariants["hit_stone"] =
		[
			GD.Load<AudioStream>("res://assets/audio/hit/stone/stone_hit_1.wav"),
			GD.Load<AudioStream>("res://assets/audio/hit/stone/stone_hit_2.wav"),
			GD.Load<AudioStream>("res://assets/audio/hit/stone/stone_hit_3.wav"),
			GD.Load<AudioStream>("res://assets/audio/hit/stone/stone_hit_4.wav")
		];

		SfxVariants["hit_flesh"] =
		[
			GD.Load<AudioStream>("res://assets/audio/hit/flesh/flesh_hit_1.wav"),
			GD.Load<AudioStream>("res://assets/audio/hit/flesh/flesh_hit_2.wav"),
			GD.Load<AudioStream>("res://assets/audio/hit/flesh/flesh_hit_3.wav"),
			GD.Load<AudioStream>("res://assets/audio/hit/flesh/flesh_hit_4.wav"),
			GD.Load<AudioStream>("res://assets/audio/hit/flesh/flesh_hit_5.wav"),
			GD.Load<AudioStream>("res://assets/audio/hit/flesh/flesh_hit_6.wav"),
			GD.Load<AudioStream>("res://assets/audio/hit/flesh/flesh_hit_7.wav"),
			GD.Load<AudioStream>("res://assets/audio/hit/flesh/flesh_hit_8.wav"),
			GD.Load<AudioStream>("res://assets/audio/hit/flesh/flesh_hit_9.wav")
		];

		SfxVariants["hit_flesh_blade"] =
		[
			GD.Load<AudioStream>("res://assets/audio/hit/flesh/flesh_hit_blade_small_1.wav"),
			GD.Load<AudioStream>("res://assets/audio/hit/flesh/flesh_hit_blade_small_2.wav"),
			GD.Load<AudioStream>("res://assets/audio/hit/flesh/flesh_hit_blade_small_3.wav"),
			GD.Load<AudioStream>("res://assets/audio/hit/flesh/flesh_hit_blade_small_4.wav"),
			GD.Load<AudioStream>("res://assets/audio/hit/flesh/flesh_hit_blade_small_5.wav"),
			GD.Load<AudioStream>("res://assets/audio/hit/flesh/flesh_hit_blade_small_6.wav")
		];

		// break sounds 
		SfxVariants["break_tree"] =
		[
			GD.Load<AudioStream>("res://assets/audio/break/tree/Tree Fall 1.wav"),
			GD.Load<AudioStream>("res://assets/audio/break/tree/Tree Fall 2.wav")
		];

		SfxVariants["break_stone"] =
		[
			GD.Load<AudioStream>("res://assets/audio/break/stone/Mine Large Rock A.wav"),
			GD.Load<AudioStream>("res://assets/audio/break/stone/Mine Large Rock B.wav"),
			GD.Load<AudioStream>("res://assets/audio/break/stone/Mine Large Rock C.wav"),
			GD.Load<AudioStream>("res://assets/audio/break/stone/Mine Large Rock D.wav")
		];

		Sfx["hit_fail"] = GD.Load<AudioStream>("res://assets/audio/hit/hit_fail.wav");

		// Swing sounds
		SfxVariants["swing_fist"] =
		[
			GD.Load<AudioStream>("res://assets/audio/tools/fist/Whoosh Fast A.wav"),
			GD.Load<AudioStream>("res://assets/audio/tools/fist/Whoosh Fast B.wav"),
			GD.Load<AudioStream>("res://assets/audio/tools/fist/Whoosh Fast C.wav"),
			GD.Load<AudioStream>("res://assets/audio/tools/fist/Whoosh Fast D.wav")
		];

		SfxVariants["swing_blade_small"] =
		[
			GD.Load<AudioStream>("res://assets/audio/tools/blade-small/blade_small_swing_1.wav"),
			GD.Load<AudioStream>("res://assets/audio/tools/blade-small/blade_small_swing_2.wav"),
			GD.Load<AudioStream>("res://assets/audio/tools/blade-small/blade_small_swing_3.wav"),
			GD.Load<AudioStream>("res://assets/audio/tools/blade-small/blade_small_swing_4.wav"),
			GD.Load<AudioStream>("res://assets/audio/tools/blade-small/blade_small_swing_5.wav")
		];

		// Footsteps
		SfxVariants["footstep_grass"] =
		[
			GD.Load<AudioStream>("res://assets/audio/footsteps/grass/grass_walk_1.wav"),
			GD.Load<AudioStream>("res://assets/audio/footsteps/grass/grass_walk_2.wav"),
			GD.Load<AudioStream>("res://assets/audio/footsteps/grass/grass_walk_3.wav"),
			GD.Load<AudioStream>("res://assets/audio/footsteps/grass/grass_walk_4.wav"),
			GD.Load<AudioStream>("res://assets/audio/footsteps/grass/grass_walk_5.wav")
		];

		SfxVariants["footstep_snow"] =
		[
			GD.Load<AudioStream>("res://assets/audio/footsteps/snow/snow_walk_1.wav"),
			GD.Load<AudioStream>("res://assets/audio/footsteps/snow/snow_walk_2.wav"),
			GD.Load<AudioStream>("res://assets/audio/footsteps/snow/snow_walk_3.wav"),
			GD.Load<AudioStream>("res://assets/audio/footsteps/snow/snow_walk_4.wav"),
			GD.Load<AudioStream>("res://assets/audio/footsteps/snow/snow_walk_5.wav")
		];

		SfxVariants["footstep_sand"] =
		[
			GD.Load<AudioStream>("res://assets/audio/footsteps/sand/sand_walk_1.wav"),
			GD.Load<AudioStream>("res://assets/audio/footsteps/sand/sand_walk_2.wav"),
			GD.Load<AudioStream>("res://assets/audio/footsteps/sand/sand_walk_3.wav"),
			GD.Load<AudioStream>("res://assets/audio/footsteps/sand/sand_walk_4.wav"),
			GD.Load<AudioStream>("res://assets/audio/footsteps/sand/sand_walk_5.wav")
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
		Music.Add(GD.Load<AudioStream>("res://assets/audio/music/the-deer-who-ran.mp3"));
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