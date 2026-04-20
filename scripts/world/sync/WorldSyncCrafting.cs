using Godot;

public partial class WorldSync
{
	public void RequestCraftItem(string recipeId)
	{
		if (string.IsNullOrEmpty(recipeId))
			return;

		RpcId(1, nameof(RequestCraftItemClient), recipeId);
	}

	public void RequestStartStationCraft(Vector2I tileCoord, string recipeId)
	{
		if (string.IsNullOrEmpty(recipeId))
			return;

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
}