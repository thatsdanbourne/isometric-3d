using Godot;

public partial class WorldSync : Node
{
	private World _world;
	private Vector2I? _lastSubmittedChunkCenter;

	public void Init(World world)
	{
		_world = world;
	}


	//
	// chunk interest/send/receive
	//
	public void SendChunkUnloadToPeer(int peerId, Vector2I chunkCoord)
	{
		var localPeerId = Multiplayer.GetUniqueId();

		if (peerId == localPeerId)
		{
			_world.ChunkManager.RemoveChunk(chunkCoord);
			return;
		}

		RpcId(peerId, nameof(ReceiveChunkUnload), chunkCoord);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void ReceiveChunkUnload(Vector2I chunkCoord)
	{
		if (!_world.ActiveChunks.ContainsKey(chunkCoord))
			return;

		_world.ChunkManager.RemoveChunk(chunkCoord);
	}

	public void SendChunkToPeer(int peerId, ChunkDto chunk)
	{
		var serialized = SerializationUtils.SerializeChunk(chunk);
		RpcId(peerId, nameof(ReceiveChunk), serialized);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	public void ReceiveChunk(Godot.Collections.Dictionary chunkData)
	{
		var chunk = SerializationUtils.DeserializeChunk(chunkData);
		_world.ChunkGenerator.EnqueueClientChunk(chunk);
	}

	public void UpdateLocalChunkInterest(Vector3 localPlayerPosition, int chunkRadius)
	{
		var center = TileUtils.WorldToChunk(localPlayerPosition);

		if (_lastSubmittedChunkCenter.HasValue && _lastSubmittedChunkCenter.Value == center)
			return;

		_lastSubmittedChunkCenter = center;

		var coords = BuildDesiredChunkArray(center, chunkRadius);

		if (Multiplayer.HasMultiplayerPeer() && !Multiplayer.IsServer()) RpcId(1, nameof(SubmitDesiredChunks), coords);
	}

	private Godot.Collections.Array<Vector2I> BuildDesiredChunkArray(Vector2I center, int chunkRadius)
	{
		var coords = new Godot.Collections.Array<Vector2I>();

		for (var x = -chunkRadius; x <= chunkRadius; x++)
		for (var y = -chunkRadius; y <= chunkRadius; y++)
			coords.Add(new Vector2I(center.X + x, center.Y + y));

		return coords;
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	public void SubmitDesiredChunks(Godot.Collections.Array<Vector2I> coords)
	{
		if (!Multiplayer.IsServer())
			return;

		var peerId = Multiplayer.GetRemoteSenderId();
		_world.ChunkManager.UpdatePeerInterest(peerId, coords);
	}

	//
	// broadcast/receive object placed/removed
	//
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	public void RequestBreakObject(Vector2I chunkCoord, Vector2I tileCoord)
	{
		if (!Multiplayer.IsServer())
			return;

		if (!_world.ActiveChunks.TryGetValue(chunkCoord, out var chunk))
			return;

		ChunkObject target = null;

		foreach (var obj in chunk.Objects)
			if (obj.TileCoord == tileCoord)
			{
				target = obj;
				break;
			}

		if (target == null)
			return;

		_world.WorldObjectManager.RequestBreak(target);
	}

	public void BroadcastObjectRemoved(Vector2I chunkCoord, Vector2I tileCoord)
	{
		if (!Multiplayer.IsServer())
			return;

		foreach (var peerId in _world.ChunkManager.GetPeersInterestedInChunk(chunkCoord))
			RpcId(peerId, nameof(ReceiveObjectRemoved), chunkCoord, tileCoord);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	public void RequestPlaceObject(string itemId, int defId, Vector2I chunkCoord, Vector2I tileCoord, Vector3 worldPos)
	{
		if (!Multiplayer.IsServer())
			return;

		var peerId = Multiplayer.GetRemoteSenderId();
		var player = _world.GetPlayerById(peerId);
		if (player == null)
			return;

		var item = ItemRegistry.GetItem(itemId) as PlaceableItem;
		if (item == null)
			return;

		var def = item.PlaceableObjectDefinition;
		if (def == null)
			return;

		_world.TryPlaceItem(player, item, defId, tileCoord, chunkCoord, worldPos);
	}

	public void BroadcastObjectPlaced(ChunkObject data)
	{
		if (!Multiplayer.IsServer())
			return;

		foreach (var peerId in _world.ChunkManager.GetPeersInterestedInChunk(data.ChunkCoord))
			RpcId(
				peerId,
				nameof(ReceiveObjectPlaced),
				data.Definition.StableId,
				data.ChunkCoord,
				data.TileCoord,
				data.Position);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void ReceiveObjectRemoved(Vector2I chunkCoord, Vector2I tileCoord)
	{
		_world.WorldObjectManager.ApplyRemoteBreak(chunkCoord, tileCoord);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void ReceiveObjectPlaced(int defId, Vector2I chunkCoord, Vector2I tileCoord, Vector3 worldPos)
	{
		_world.WorldObjectManager.ApplyRemotePlace(defId, chunkCoord, tileCoord, worldPos);
	}

	//
	// player inventory state sync
	//
	public void SyncPlayerInventoryState(Player player)
	{
		if (!Multiplayer.IsServer())
			return;

		var data = SerializationUtils.SerializePlayerInventoryState(player);
		var localPeerId = Multiplayer.GetUniqueId();

		if (player.PlayerId == localPeerId)
		{
			ApplyPlayerInventoryState(data);
			return;
		}

		RpcId(player.PlayerId, nameof(ReceivePlayerInventoryState), data);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void ReceivePlayerInventoryState(Godot.Collections.Dictionary data)
	{
		ApplyPlayerInventoryState(data);
	}

	private void ApplyPlayerInventoryState(Godot.Collections.Dictionary data)
	{
		var player = GameManager.Instance.LocalPlayer;
		if (player == null || !IsInstanceValid(player))
			return;

		var state = SerializationUtils.DeserializePlayerInventoryState(data);

		for (var i = 0; i < player.Inventory.SlotCount; i++)
			player.Inventory.SetSlot(i, i < state.Inventory.Length ? state.Inventory[i] : null);

		for (var i = 0; i < player.Hotbar.SlotCount; i++)
			player.Hotbar.SetSlot(i, i < state.Hotbar.Length ? state.Hotbar[i] : null);

		player.DraggedStack = state.DraggedStack;

		player.HUD.RefreshUI();
		player.HUD.UpdateDraggedCursorFromPlayerState();
	}

	//
	// storage container state sync
	//
	private void BroadcastStorageState(StorageStateData state)
	{
		if (!Multiplayer.IsServer())
			return;

		var chunkCoord = TileUtils.WorldToChunk(TileUtils.TileToWorld(state.TileCoord));
		var serialised = SerializationUtils.SerializeStorageState(state);

		foreach (var peerId in _world.ChunkManager.GetPeersInterestedInChunk(chunkCoord))
			RpcId(peerId, nameof(BindStorageState), serialised);

		BindStorageState(serialised);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true)]
	private void BindStorageState(Godot.Collections.Dictionary stateData)
	{
		var state = SerializationUtils.DeserializeStorageState(stateData);

		var chunkCoord = TileUtils.WorldToChunk(TileUtils.TileToWorld(state.TileCoord));
		var delta = _world.MutateChunkDelta(chunkCoord);
		delta.StorageStates[state.TileCoord] = state;

		var worldObject = _world.ResolveWorldObject(state.TileCoord);
		if (worldObject is IItemContainer container)
			container.BindState(state);
	}

	private IItemContainer ResolvePlayerContainer(Player player, ContainerKind kind)
	{
		return kind switch
		{
			ContainerKind.Inventory => player.Inventory,
			ContainerKind.Hotbar => player.Hotbar,
			_ => null
		};
	}

	public StorageStateData GetOrCreateStorageState(Vector2I tileCoord)
	{
		var chunkCoord = TileUtils.WorldToChunk(TileUtils.TileToWorld(tileCoord));
		var delta = _world.GetOrCreateChunkDelta(chunkCoord);

		if (delta.StorageStates.TryGetValue(tileCoord, out var state))
			return state;

		var chunkObject = _world.ResolveChunkObject(tileCoord);
		if (chunkObject == null)
			return null;

		state = new StorageStateData
		{
			ObjectId = chunkObject.Definition.StableId,
			TileCoord = tileCoord,
			Slots = new ItemStack[9]
		};

		delta.StorageStates[tileCoord] = state;
		_world.ChunkManager.InvalidateServerChunk(chunkCoord);
		return state;
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	public void RequestContainerClick(
		int containerKind,
		int storageTileX,
		int storageTileY,
		int slotIndex,
		int mouseButton,
		bool shiftHeld)
	{
		if (!Multiplayer.IsServer())
			return;

		var peerId = Multiplayer.GetRemoteSenderId();
		var player = _world.GetPlayerById(peerId);
		if (player == null)
			return;

		var kind = (ContainerKind)containerKind;
		var storageTileCoord = new Vector2I(storageTileX, storageTileY);

		HandleContainerClickRequest(player, kind, storageTileCoord, slotIndex, mouseButton, shiftHeld);
	}

	//
	// crafting/collecting requests
	//
	public void RequestCraftItem(string recipeId)
	{
		if (string.IsNullOrEmpty(recipeId))
			return;

		if (Multiplayer.IsServer())
		{
			HandleCraftItemRequest(Multiplayer.GetUniqueId(), recipeId);
			return;
		}

		RpcId(1, nameof(RequestCraftItemClient), recipeId);
	}

	public void RequestStartStationCraft(Vector2I tileCoord, string recipeId)
	{
		if (string.IsNullOrEmpty(recipeId))
			return;

		if (Multiplayer.IsServer())
		{
			HandleStartStationCraftRequest(Multiplayer.GetUniqueId(), tileCoord, recipeId);
			return;
		}

		RpcId(1, nameof(RequestStartStationCraftClient), tileCoord, recipeId);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	private void RequestCraftItemClient(string recipeId)
	{
		if (!Multiplayer.IsServer())
			return;

		var peerId = Multiplayer.GetRemoteSenderId();
		HandleCraftItemRequest(peerId, recipeId);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	private void RequestStartStationCraftClient(Vector2I tileCoord, string recipeId)
	{
		if (!Multiplayer.IsServer())
			return;

		var peerId = Multiplayer.GetRemoteSenderId();
		HandleStartStationCraftRequest(peerId, tileCoord, recipeId);
	}

	private void HandleCraftItemRequest(int peerId, string recipeId)
	{
		var player = _world.GetPlayerById(peerId);
		if (player == null)
			return;

		var recipe = CraftingRegistry.GetRecipe(recipeId);
		if (recipe == null)
			return;

		if (!CraftingManager.Instance.ExecuteCraftRequest(player, recipe))
			return;

		SyncPlayerInventoryState(player);
	}

	private void HandleStartStationCraftRequest(int peerId, Vector2I tileCoord, string recipeId)
	{
		var player = _world.GetPlayerById(peerId);
		if (player == null)
			return;

		var recipe = CraftingRegistry.GetRecipe(recipeId);
		if (recipe == null)
			return;

		var chunkObject = _world.ResolveChunkObject(tileCoord);
		if (chunkObject == null)
			return;

		if (!CraftingManager.Instance.CanCraft(player, recipe))
			return;

		var state = GetOrCreateStationState(tileCoord, chunkObject.Definition.StableId);

		AdvanceStationProgress(state);

		if (!TryQueueStationRecipe(state, recipe))
			return;

		CraftingManager.Instance.ConsumeIngredients(player, recipe);

		var chunkCoord = TileUtils.WorldToChunk(TileUtils.TileToWorld(tileCoord));
		_world.ChunkManager.InvalidateServerChunk(chunkCoord);

		SyncPlayerInventoryState(player);
		SyncStationState(tileCoord);
	}

	private bool TryQueueStationRecipe(StationStateData state, CraftingRecipe recipe)
	{
		if (state == null || recipe == null)
			return false;

		if (!string.IsNullOrEmpty(state.ActiveRecipeId) && state.ActiveRecipeId != recipe.Id)
			return false;

		if (string.IsNullOrEmpty(state.ActiveRecipeId))
		{
			state.IsCrafting = true;
			state.ActiveRecipeId = recipe.Id;
			state.TotalCount = 0;
			state.CompletedCount = 0;
			state.TimeRemaining = recipe.CraftTime;
		}
		else if (!state.IsCrafting)
		{
			state.IsCrafting = true;
			state.TimeRemaining = recipe.CraftTime;
		}

		state.TotalCount += 1;
		state.LastUpdateTime = _world.WorldTimeSeconds;
		return true;
	}

	public bool TryGetStationState(Vector2I tileCoord, out StationStateData state)
	{
		var chunkCoord = TileUtils.WorldToChunk(TileUtils.TileToWorld(tileCoord));
		state = null;

		if (!_world.TryGetChunkDelta(chunkCoord, out var delta))
			return false;

		return delta.StationStates.TryGetValue(tileCoord, out state);
	}

	public StationStateData GetOrCreateStationState(Vector2I tileCoord, int objectId)
	{
		var chunkCoord = TileUtils.WorldToChunk(TileUtils.TileToWorld(tileCoord));
		var delta = _world.GetOrCreateChunkDelta(chunkCoord);

		if (delta.StationStates.TryGetValue(tileCoord, out var state))
			return state;

		state = new StationStateData
		{
			ObjectId = objectId,
			TileCoord = tileCoord,
			ActiveRecipeId = string.Empty,
			TimeRemaining = 0f,
			CompletedCount = 0,
			TotalCount = 0,
			IsCrafting = false,
			LastUpdateTime = _world.WorldTimeSeconds
		};

		delta.StationStates[tileCoord] = state;
		_world.ChunkManager.InvalidateServerChunk(chunkCoord);
		return state;
	}

	public void AdvanceStationProgress(StationStateData state)
	{
		if (state is not { IsCrafting: true } || string.IsNullOrEmpty(state.ActiveRecipeId))
			return;

		var recipe = CraftingRegistry.GetRecipe(state.ActiveRecipeId);
		if (recipe == null || recipe.CraftTime <= 0f)
			return;

		var now = _world.WorldTimeSeconds;
		var elapsed = now - state.LastUpdateTime;
		if (elapsed <= 0f)
			return;

		state.TimeRemaining -= (float)elapsed;

		while (state.TimeRemaining <= 0f && state.CompletedCount < state.TotalCount)
		{
			state.CompletedCount++;

			if (state.CompletedCount >= state.TotalCount)
			{
				state.IsCrafting = false;
				state.TimeRemaining = 0f;
				break;
			}

			state.TimeRemaining += recipe.CraftTime;
		}

		state.LastUpdateTime = now;

		var chunkCoord = TileUtils.WorldToChunk(TileUtils.TileToWorld(state.TileCoord));
		var delta = _world.MutateChunkDelta(chunkCoord);
		delta.StationStates[state.TileCoord] = state;
	}

	public StationStateData ResolveStationState(Vector2I tileCoord)
	{
		if (TryGetStationState(tileCoord, out var state))
		{
			AdvanceStationProgress(state);
			return state;
		}

		return null;
	}

	private void SyncStationState(Vector2I stationTileCoord)
	{
		var state = ResolveStationState(stationTileCoord);
		if (state == null)
			return;

		Rpc(nameof(BindStationState),
			state.ObjectId,
			state.TileCoord,
			state.ActiveRecipeId,
			state.TimeRemaining,
			state.CompletedCount,
			state.TotalCount,
			state.IsCrafting,
			state.LastUpdateTime);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true)]
	private void BindStationState(
		int objectId,
		Vector2I tileCoord,
		string activeRecipeId,
		float timeRemaining,
		int completedCount,
		int totalCount,
		bool isCrafting,
		double lastUpdateTime)
	{
		var state = new StationStateData
		{
			ObjectId = objectId,
			TileCoord = tileCoord,
			ActiveRecipeId = activeRecipeId,
			TimeRemaining = timeRemaining,
			CompletedCount = completedCount,
			TotalCount = totalCount,
			IsCrafting = isCrafting,
			LastUpdateTime = lastUpdateTime
		};

		var chunkCoord = TileUtils.WorldToChunk(TileUtils.TileToWorld(tileCoord));
		var delta = _world.MutateChunkDelta(chunkCoord);
		delta.StationStates[tileCoord] = state;

		var worldObject = _world.ResolveWorldObject(tileCoord);
		if (worldObject is IProcessingStation station)
			station.BindState(state);
	}

	public void RequestCollectStationOutput(Vector2I tileCoord)
	{
		if (Multiplayer.IsServer())
		{
			HandleCollectStationOutputRequest(Multiplayer.GetUniqueId(), tileCoord);
			return;
		}

		RpcId(1, nameof(RequestCollectStationOutputClient), tileCoord);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	private void RequestCollectStationOutputClient(Vector2I tileCoord)
	{
		if (!Multiplayer.IsServer())
			return;

		var peerId = Multiplayer.GetRemoteSenderId();
		HandleCollectStationOutputRequest(peerId, tileCoord);
	}

	private void HandleCollectStationOutputRequest(int peerId, Vector2I tileCoord)
	{
		var player = _world.GetPlayerById(peerId);
		if (player == null)
			return;

		if (!TryGetStationState(tileCoord, out var state))
			return;

		AdvanceStationProgress(state);

		if (state.CompletedCount <= 0)
			return;

		var recipe = CraftingRegistry.GetRecipe(state.ActiveRecipeId);
		if (recipe == null)
			return;

		var item = ItemRegistry.GetItem(recipe.ResultItemId);
		InventoryManager.Instance.AddItem(player, item, state.CompletedCount);

		state.TotalCount -= state.CompletedCount;
		state.CompletedCount = 0;

		if (state.TotalCount <= 0)
		{
			state.TotalCount = 0;
			state.CompletedCount = 0;
			state.TimeRemaining = 0f;
			state.IsCrafting = false;
			state.ActiveRecipeId = string.Empty;
			state.LastUpdateTime = _world.WorldTimeSeconds;
		}
		else
		{
			state.IsCrafting = true;
			state.LastUpdateTime = _world.WorldTimeSeconds;
		}

		var chunkCoord = TileUtils.WorldToChunk(TileUtils.TileToWorld(tileCoord));
		_world.ChunkManager.InvalidateServerChunk(chunkCoord);

		SyncPlayerInventoryState(player);
		SyncStationState(tileCoord);
	}

	//
	// container click requests
	//
	private void HandleContainerClickRequest(
		Player player,
		ContainerKind kind,
		Vector2I storageTileCoord,
		int slotIndex,
		int mouseButton,
		bool shiftHeld
	)
	{
		if (kind == ContainerKind.Storage)
			HandleStorageClickRequest(player, storageTileCoord, slotIndex, mouseButton, shiftHeld);
		else
			HandlePlayerContainerClickRequest(player, kind, slotIndex, mouseButton, shiftHeld);

		SyncPlayerInventoryState(player);
	}

	private void HandlePlayerContainerClickRequest(Player player, ContainerKind kind, int slotIndex, int mouseButton,
		bool shiftHeld)
	{
		var container = ResolvePlayerContainer(player, kind);
		if (container == null)
			return;

		if (slotIndex < 0 || slotIndex >= container.SlotCount)
			return;

		if (shiftHeld)
		{
			switch (kind)
			{
				case ContainerKind.Inventory:
					InventoryManager.Instance.ShiftClick(
						container,
						slotIndex,
						player.Hotbar
					);
					break;
				case ContainerKind.Hotbar:
					InventoryManager.Instance.ShiftClick(
						container,
						slotIndex,
						player.Inventory
					);
					break;
			}
		}
		else
		{
			if (mouseButton == 0)
				player.DraggedStack = InventoryManager.Instance.LeftClick(
					container,
					slotIndex,
					player.DraggedStack
				);
			else
				player.DraggedStack = InventoryManager.Instance.RightClick(
					container,
					slotIndex,
					player.DraggedStack
				);
		}
	}

	private void HandleStorageClickRequest(Player player, Vector2I storageTileCoord, int slotIndex, int mouseButton,
		bool shiftHeld)
	{
		var storageState = GetOrCreateStorageState(storageTileCoord);
		if (storageState == null)
			return;

		if (slotIndex < 0 || slotIndex >= storageState.Slots.Length)
			return;

		if (shiftHeld)
		{
			HandleStorageShiftClick(player, storageState, slotIndex);
		}
		else
		{
			if (mouseButton == 0)
				player.DraggedStack = LeftClickStorage(storageState, slotIndex, player.DraggedStack);
			else
				player.DraggedStack = RightClickStorage(storageState, slotIndex, player.DraggedStack);
		}

		var chunkCoord = TileUtils.WorldToChunk(TileUtils.TileToWorld(storageTileCoord));
		var delta = _world.GetOrCreateChunkDelta(chunkCoord);
		delta.StorageStates[storageTileCoord] = storageState;

		_world.ChunkManager.InvalidateServerChunk(chunkCoord);
		BroadcastStorageState(storageState);
	}

	public void HandleContainerClick(
		ContainerKind kind,
		Vector2I storageTileCoord,
		int slotIndex,
		int mouseButton,
		bool shiftHeld)
	{
		var isServerAuthority = !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer();
		var localPlayer = GameManager.Instance.LocalPlayer;

		if (localPlayer == null || !IsInstanceValid(localPlayer))
			return;

		if (isServerAuthority)
		{
			HandleContainerClickRequest(localPlayer, kind, storageTileCoord, slotIndex, mouseButton, shiftHeld);
			localPlayer.HUD.RefreshUI();
			return;
		}

		RpcId(
			1,
			nameof(RequestContainerClick),
			(int)kind,
			storageTileCoord.X,
			storageTileCoord.Y,
			slotIndex,
			mouseButton,
			shiftHeld
		);
	}

	private ItemStack LeftClickStorage(StorageStateData storageState, int slotIndex, ItemStack draggedStack)
	{
		var slots = storageState.Slots;
		var slotStack = slots[slotIndex];

		if (draggedStack == null)
		{
			slots[slotIndex] = null;
			return slotStack;
		}

		if (slotStack == null)
		{
			slots[slotIndex] = draggedStack;
			return null;
		}

		if (slotStack.Item.Id == draggedStack.Item.Id && slotStack.Count < slotStack.Item.StackSize)
		{
			var transfer = Mathf.Min(draggedStack.Count, slotStack.Item.StackSize - slotStack.Count);
			slotStack.Count += transfer;
			draggedStack.Count -= transfer;

			if (draggedStack.Count <= 0)
				return null;

			return draggedStack;
		}

		slots[slotIndex] = draggedStack;
		return slotStack;
	}

	private ItemStack RightClickStorage(StorageStateData storageState, int slotIndex, ItemStack draggedStack)
	{
		var slots = storageState.Slots;
		var slotStack = slots[slotIndex];

		if (draggedStack == null)
		{
			if (slotStack == null)
				return null;

			var takeAmount = Mathf.CeilToInt(slotStack.Count / 2.0f);
			var taken = new ItemStack(slotStack.Item, takeAmount);

			slotStack.Count -= takeAmount;
			if (slotStack.Count <= 0)
				slots[slotIndex] = null;

			return taken;
		}

		if (slotStack == null)
		{
			slots[slotIndex] = new ItemStack(draggedStack.Item, 1);
			draggedStack.Count -= 1;

			if (draggedStack.Count <= 0)
				return null;

			return draggedStack;
		}

		if (slotStack.Item.Id == draggedStack.Item.Id && slotStack.Count < slotStack.Item.StackSize)
		{
			slotStack.Count += 1;
			draggedStack.Count -= 1;

			if (draggedStack.Count <= 0)
				return null;
		}

		return draggedStack;
	}

	private void HandleStorageShiftClick(Player player, StorageStateData storageState, int slotIndex)
	{
		var slots = storageState.Slots;
		var stack = slots[slotIndex];
		if (stack == null)
			return;

		var remaining = InventoryManager.Instance.AddItem(player, stack.Item, stack.Count);

		if (remaining > 0)
			slots[slotIndex].Count = remaining;
		else
			slots[slotIndex] = null;
	}

	//
	// item drops
	//
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	public void RequestDropItem(string itemId, int count)
	{
		if (!Multiplayer.IsServer())
			return;

		if (count <= 0)
			return;

		var senderId = Multiplayer.GetRemoteSenderId();
		var player = _world.GetPlayerById(senderId);
		if (player == null)
			return;

		_world.WorldObjectManager.HandleDropItemRequest(player, itemId, count);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	public void SpawnRemotePickups(Godot.Collections.Array<Godot.Collections.Dictionary> pickups)
	{
		foreach (var p in pickups) _world.WorldObjectManager.SpawnPickupFromData(p);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	public void RequestPickup(ulong pickupId)
	{
		if (!Multiplayer.IsServer())
			return;

		var senderId = Multiplayer.GetRemoteSenderId();
		var player = _world.GetPlayerById(senderId);

		if (player == null)
			return;

		_world.WorldObjectManager.HandlePickupRequest(player, pickupId);
	}

	public void BroadcastPickupRemoved(ItemPickup pickup)
	{
		var chunk = TileUtils.WorldToChunk(pickup.GlobalPosition);
		var peers = _world.ChunkManager.GetPeersInterestedInChunk(chunk);

		foreach (var peerId in peers)
			RpcId(peerId, nameof(RemoveRemotePickup), pickup.PickupId);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void RemoveRemotePickup(ulong pickupId)
	{
		_world.WorldObjectManager.RemovePickupById(pickupId);
	}
}