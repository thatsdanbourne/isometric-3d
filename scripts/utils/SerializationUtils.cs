using Godot;
using System;
using System.Collections.Generic;

public static class SerializationUtils
{
	public static Godot.Collections.Dictionary SerializeItemStack(ItemStack stack)
	{
		return new Godot.Collections.Dictionary
		{
			["item_id"] = stack?.Item.Id ?? "",
			["count"] = stack?.Count ?? 0
		};
	}

	public static ItemStack DeserializeItemStack(Godot.Collections.Dictionary source)
	{
		var itemId = (string)source["item_id"];
		var count = (int)source["count"];

		return itemId == "" || count <= 0
			? null
			: new ItemStack(ItemRegistry.GetItem(itemId), count);
	}

	public static Godot.Collections.Array SerializeSlots(ItemStack[] slots)
	{
		var arr = new Godot.Collections.Array();

		if (slots == null)
			return arr;

		foreach (var stack in slots)
			arr.Add(SerializeItemStack(stack));

		return arr;
	}

	public static ItemStack[] DeserializeSlots(Godot.Collections.Array source)
	{
		var slots = new ItemStack[source.Count];

		for (var i = 0; i < source.Count; i++)
			slots[i] = DeserializeItemStack((Godot.Collections.Dictionary)source[i]);

		return slots;
	}

	public static Godot.Collections.Dictionary SerializePlayerInventoryState(Player player)
	{
		return new Godot.Collections.Dictionary
		{
			["inventory"] = SerializeSlots(player.Inventory.GetSlots()),
			["hotbar"] = SerializeSlots(player.Hotbar.GetSlots()),
			["dragged"] = SerializeItemStack(player.DraggedStack)
		};
	}

	public static PlayerInventoryStateData DeserializePlayerInventoryState(Godot.Collections.Dictionary data)
	{
		return new PlayerInventoryStateData
		{
			Inventory = DeserializeSlots((Godot.Collections.Array)data["inventory"]),
			Hotbar = DeserializeSlots((Godot.Collections.Array)data["hotbar"]),
			DraggedStack = DeserializeItemStack((Godot.Collections.Dictionary)data["dragged"])
		};
	}

	public static Godot.Collections.Dictionary SerializeStorageState(StorageStateData state)
	{
		var dict = new Godot.Collections.Dictionary
		{
			["object_id"] = state.ObjectId,
			["tile_x"] = state.TileCoord.X,
			["tile_y"] = state.TileCoord.Y
		};

		var slots = SerializeSlots(state.Slots);

		dict["slots"] = slots;
		return dict;
	}

	public static StorageStateData DeserializeStorageState(Godot.Collections.Dictionary dict)
	{
		var state = new StorageStateData
		{
			ObjectId = (int)dict["object_id"],
			TileCoord = new Vector2I(
				(int)dict["tile_x"],
				(int)dict["tile_y"]
			)
		};

		state.Slots = DeserializeSlots((Godot.Collections.Array)dict["slots"]);

		return state;
	}

	public static Godot.Collections.Dictionary SerializeStationState(StationStateData state)
	{
		return new Godot.Collections.Dictionary
		{
			["object_id"] = state.ObjectId,
			["tile_x"] = state.TileCoord.X,
			["tile_y"] = state.TileCoord.Y,
			["active_recipe_id"] = state.ActiveRecipeId ?? "",
			["time_remaining"] = state.TimeRemaining,
			["completed_count"] = state.CompletedCount,
			["total_count"] = state.TotalCount,
			["is_crafting"] = state.IsCrafting,
			["last_update_time"] = state.LastUpdateTime
		};
	}

	public static StationStateData DeserializeStationState(Godot.Collections.Dictionary dict)
	{
		return new StationStateData
		{
			ObjectId = (int)dict["object_id"],
			TileCoord = new Vector2I(
				(int)dict["tile_x"],
				(int)dict["tile_y"]
			),
			ActiveRecipeId = (string)dict["active_recipe_id"],
			TimeRemaining = (float)dict["time_remaining"],
			CompletedCount = (int)dict["completed_count"],
			TotalCount = (int)dict["total_count"],
			IsCrafting = (bool)dict["is_crafting"],
			LastUpdateTime = (double)dict["last_update_time"]
		};
	}

	public static Godot.Collections.Dictionary SerializeChunk(ChunkDto chunk)
	{
		var dict = new Godot.Collections.Dictionary
		{
			["coord_x"] = chunk.Coord.X,
			["coord_y"] = chunk.Coord.Y
		};

		var tiles = new Godot.Collections.Array();
		foreach (var tile in chunk.Tiles)
			tiles.Add(new Godot.Collections.Dictionary
			{
				["x"] = tile.X,
				["y"] = tile.Y,
				["definition_id"] = tile.DefinitionId,
				["biome_id"] = tile.BiomeId,
				["temperature"] = tile.Temperature,
				["humidity"] = tile.Humidity
			});

		var objects = new Godot.Collections.Array();
		foreach (var obj in chunk.Objects)
			objects.Add(new Godot.Collections.Dictionary
			{
				["definition_id"] = obj.DefinitionId,
				["chunk_x"] = obj.ChunkCoord.X,
				["chunk_y"] = obj.ChunkCoord.Y,
				["tile_x"] = obj.TileCoord.X,
				["tile_y"] = obj.TileCoord.Y,
				["pos_x"] = obj.Position.X,
				["pos_y"] = obj.Position.Y,
				["pos_z"] = obj.Position.Z,
				["source"] = (int)obj.Source
			});

		var storageStates = new Godot.Collections.Array();
		foreach (var kv in chunk.StorageStates)
			storageStates.Add(SerializeStorageState(kv.Value));

		var stationStates = new Godot.Collections.Array();
		foreach (var kv in chunk.StationStates)
			stationStates.Add(SerializeStationState(kv.Value));

		dict["tiles"] = tiles;
		dict["objects"] = objects;
		dict["storage_states"] = storageStates;
		dict["station_states"] = stationStates;

		return dict;
	}

	public static ChunkDto DeserializeChunk(Godot.Collections.Dictionary dict)
	{
		var coord = new Vector2I(
			(int)dict["coord_x"],
			(int)dict["coord_y"]
		);

		var tiles = new List<TileInstanceDto>();
		var tileArray = (Godot.Collections.Array)dict["tiles"];
		foreach (Godot.Collections.Dictionary tileDict in tileArray)
			tiles.Add(new TileInstanceDto
			{
				X = (int)tileDict["x"],
				Y = (int)tileDict["y"],
				DefinitionId = (int)tileDict["definition_id"],
				BiomeId = (int)tileDict["biome_id"],
				Temperature = (float)tileDict["temperature"],
				Humidity = (float)tileDict["humidity"]
			});

		var objects = new List<ChunkObjectDto>();
		var objectArray = (Godot.Collections.Array)dict["objects"];
		foreach (Godot.Collections.Dictionary objDict in objectArray)
			objects.Add(new ChunkObjectDto
			{
				DefinitionId = (int)objDict["definition_id"],
				ChunkCoord = new Vector2I(
					(int)objDict["chunk_x"],
					(int)objDict["chunk_y"]
				),
				TileCoord = new Vector2I(
					(int)objDict["tile_x"],
					(int)objDict["tile_y"]
				),
				Position = new Vector3(
					(float)objDict["pos_x"],
					(float)objDict["pos_y"],
					(float)objDict["pos_z"]
				),
				Source = (ChunkObjectSource)(int)objDict["source"]
			});

		var chunk = new ChunkDto(coord, tiles, objects);

		if (dict.ContainsKey("storage_states"))
		{
			var storageArray = (Godot.Collections.Array)dict["storage_states"];
			foreach (Godot.Collections.Dictionary storageDict in storageArray)
			{
				var state = DeserializeStorageState(storageDict);
				chunk.StorageStates[state.TileCoord] = state;
			}
		}

		if (dict.ContainsKey("station_states"))
		{
			var stationArray = (Godot.Collections.Array)dict["station_states"];
			foreach (Godot.Collections.Dictionary stationDict in stationArray)
			{
				var state = DeserializeStationState(stationDict);
				chunk.StationStates[state.TileCoord] = state;
			}
		}

		return chunk;
	}
}