using Godot;

public partial class WorldSync
{
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

		_world.WorldObjectManager.TryGetObject(chunkCoord, state.TileCoord, out var worldObject);
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

		var player = GetRequestingPlayer();
		if (player == null)
			return;

		var kind = (ContainerKind)containerKind;
		var storageTileCoord = new Vector2I(storageTileX, storageTileY);

		HandleContainerClickRequest(player, kind, storageTileCoord, slotIndex, mouseButton, shiftHeld);
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
}