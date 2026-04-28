using Godot;
using System;
using System.Collections.Generic;
using Godot.Collections;
using Array = Godot.Collections.Array;

public static class SerializationUtils
{
	#region Inventory and slots

	public static Dictionary SerializeItemStack(ItemStack stack)
	{
		return new Dictionary
		{
			["item_id"] = stack?.Item.Id ?? "",
			["count"] = stack?.Count ?? 0
		};
	}

	public static ItemStack DeserializeItemStack(Dictionary source)
	{
		var itemId = (string)source["item_id"];
		var count = (int)source["count"];

		return itemId == "" || count <= 0
			? null
			: new ItemStack(ItemRegistry.GetItem(itemId), count);
	}

	public static Array SerializeSlots(ItemStack[] slots)
	{
		var arr = new Array();

		if (slots == null)
			return arr;

		foreach (var stack in slots)
			arr.Add(SerializeItemStack(stack));

		return arr;
	}

	public static ItemStack[] DeserializeSlots(Array source)
	{
		var slots = new ItemStack[source.Count];

		for (var i = 0; i < source.Count; i++)
			slots[i] = DeserializeItemStack((Dictionary)source[i]);

		return slots;
	}

	public static Dictionary SerializePlayerInventoryState(Player player)
	{
		return new Dictionary
		{
			["inventory"] = SerializeSlots(player.Inventory.GetSlots()),
			["hotbar"] = SerializeSlots(player.Hotbar.GetSlots()),
			["dragged"] = SerializeItemStack(player.DraggedStack)
		};
	}

	public static PlayerInventoryStateData DeserializePlayerInventoryState(Dictionary data)
	{
		return new PlayerInventoryStateData
		{
			Inventory = DeserializeSlots((Array)data["inventory"]),
			Hotbar = DeserializeSlots((Array)data["hotbar"]),
			DraggedStack = DeserializeItemStack((Dictionary)data["dragged"])
		};
	}

	#endregion

	#region Storage and station states

	public static Dictionary SerializeStorageState(StorageStateData state)
	{
		var dict = new Dictionary
		{
			["object_id"] = state.ObjectId,
			["tile_x"] = state.TileCoord.X,
			["tile_y"] = state.TileCoord.Y
		};

		var slots = SerializeSlots(state.Slots);

		dict["slots"] = slots;
		return dict;
	}

	public static StorageStateData DeserializeStorageState(Dictionary dict)
	{
		var state = new StorageStateData
		{
			ObjectId = (int)dict["object_id"],
			TileCoord = new Vector2I(
				(int)dict["tile_x"],
				(int)dict["tile_y"]
			)
		};

		state.Slots = DeserializeSlots((Array)dict["slots"]);

		return state;
	}

	public static Dictionary SerializeStationState(StationStateData state)
	{
		return new Dictionary
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

	public static StationStateData DeserializeStationState(Dictionary dict)
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

	#endregion

	#region Chunks

	public static Dictionary SerializeChunk(Chunk chunk)
	{
		var dict = new Dictionary
		{
			["coord_x"] = chunk.Coord.X,
			["coord_y"] = chunk.Coord.Y
		};

		var tiles = new Array();

		var c = chunk.Tiles.GetLength(0);

		for (var y = 0; y < c; y++)
		for (var x = 0; x < c; x++)
		{
			var tile = chunk.Tiles[x, y];

			tiles.Add((int)tile.Definition.Id);
			tiles.Add((int)tile.Biome);
			tiles.Add(tile.Temp);
			tiles.Add(tile.Humidity);
		}

		dict["tiles"] = tiles;

		var objects = new Array();

		foreach (var obj in chunk.Objects)
			objects.Add(new Dictionary
			{
				["definition_id"] = obj.Definition.StableId,
				["tile_x"] = obj.TileCoord.X,
				["tile_y"] = obj.TileCoord.Y,
				["pos_x"] = obj.Position.X,
				["pos_y"] = obj.Position.Y,
				["pos_z"] = obj.Position.Z,
				["source"] = (int)obj.Source
			});

		dict["objects"] = objects;

		if (chunk.StorageStates.Count > 0)
		{
			var storageStates = new Array();

			foreach (var state in chunk.StorageStates.Values)
				storageStates.Add(SerializeStorageState(state));

			dict["storage_states"] = storageStates;
		}

		if (chunk.StationStates.Count > 0)
		{
			var stationStates = new Array();
			foreach (var state in chunk.StationStates.Values)
				stationStates.Add(SerializeStationState(state));

			if (chunk.StationStates.Count > 0)
				dict["station_states"] = stationStates;
		}

		return dict;
	}

	public static Chunk DeserializeChunk(Dictionary dict, int chunkSize = 16)
	{
		var coord = new Vector2I(
			(int)dict["coord_x"],
			(int)dict["coord_y"]
		);

		var c = chunkSize;
		var tiles = new TileInstance[c, c];

		var tileArray = (Array)dict["tiles"];

		var i = 0;
		for (var y = 0; y < c; y++)
		for (var x = 0; x < c; x++)
		{
			var definitionId = (int)tileArray[i++];
			var biomeId = (int)tileArray[i++];
			var temp = (float)tileArray[i++];
			var humidity = (float)tileArray[i++];

			var tileDef = TileRegistry.Get((TileId)definitionId);

			tiles[x, y] = new TileInstance(
				tileDef,
				(BiomeId)biomeId,
				temp,
				humidity
			);
		}

		var objects = new List<ChunkObject>();

		var objectArray = (Array)dict["objects"];
		foreach (Dictionary objDict in objectArray)
		{
			var def = WorldObjectRegistry.GetDefinition((int)objDict["definition_id"]);

			objects.Add(new ChunkObject
			{
				Definition = def,
				ChunkCoord = coord,
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
		}

		var chunk = new Chunk(coord, tiles, objects);

		if (dict.TryGetValue("storage_states", out var storageStates))
		{
			var storageArray = (Array)storageStates;
			foreach (Dictionary storageDict in storageArray)
			{
				var state = DeserializeStorageState(storageDict);
				chunk.StorageStates[state.TileCoord] = state;
			}
		}

		if (dict.TryGetValue("station_states", out var stationStates))
		{
			var stationArray = (Array)stationStates;
			foreach (Dictionary stationDict in stationArray)
			{
				var state = DeserializeStationState(stationDict);
				chunk.StationStates[state.TileCoord] = state;
			}
		}

		return chunk;
	}

	#endregion

	#region Item Drops

	public static Dictionary SerializePickup(ulong id, string itemId, int count, Vector3 pos,
		Vector3 vel,
		float vVel)
	{
		return new Dictionary
		{
			{ "id", id },
			{ "item_id", itemId },
			{ "count", count },
			{ "pos", pos },
			{ "vel", vel },
			{ "v_vel", vVel }
		};
	}

	public static ItemPickupSpawnData DeserializePickup(Dictionary dict)
	{
		return new ItemPickupSpawnData
		{
			PickupId = (ulong)dict["id"],
			ItemId = (string)dict["item_id"],
			Count = (int)dict["count"],
			Position = (Vector3)dict["pos"],
			InitialVelocity = (Vector3)dict["vel"],
			InitialVerticalVelocity = (float)dict["v_vel"]
		};
	}

	#endregion
}