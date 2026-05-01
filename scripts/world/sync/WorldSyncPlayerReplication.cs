using Godot;

public partial class WorldSync
{
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

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	public void SyncSelectedHotbarSlot(int playerId, int slotIndex)
	{
		var player = _world.GetPlayerById(playerId);
		player?.ApplyRemoteSelectedSlot(slotIndex);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	public void SyncHeldItem(int playerId, int slotIndex, string itemId)
	{
		var player = _world.GetPlayerById(playerId);
		player?.ApplyRemoteHeldItem(slotIndex, itemId);
	}

	public void SyncHeldItemToPeer(int peerId, Player player)
	{
		if (player == null)
			return;

		RpcId(peerId,
			nameof(SyncHeldItem),
			player.PlayerId,
			player.Hotbar.SelectedSlot,
			player.GetActiveTool().Id);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	public void PlayRemoteUseActiveToolVisual(int playerId, string toolId, Vector3 swingDir)
	{
		var player = _world.GetPlayerById(playerId);
		if (player == null || player.PlayerId == Multiplayer.GetUniqueId())
			return;

		if (ItemRegistry.GetItem(toolId) is not ToolItem item)
			return;

		player.PlayRemoteUseActiveToolVisual(item, swingDir);
	}

	public void BroadcastAttackWorldResult(ToolHitResult result)
	{
		Rpc(
			nameof(ReceiveAttackWorldResult),
			(int)result.Outcome,
			result.TargetType,
			result.HitSoundKey,
			result.BreakSoundKey,
			result.HitPoint
		);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	public void ReceiveAttackWorldResult(int outcome, string targetType, string hitSoundKey,
		string breakSoundsKey,
		Vector3 hitPoint)
	{
		var result = new ToolHitResult((ToolHitOutcome)outcome, targetType, hitSoundKey, breakSoundsKey, hitPoint);
		GameManager.Instance.LocalPlayer?.HandleAttackResult(result);
	}

	public void SendPlayerHitState(Player player)
	{
		if (player == null)
			return;

		RpcId(
			player.PlayerId,
			nameof(ReceivePlayerHitState),
			player.Health,
			player.GlobalPosition,
			player.KnockbackVelocity
		);
	}

	public void SendLocalAttackResult(int playerId, ToolHitResult result)
	{
		RpcId(
			playerId,
			nameof(ReceiveLocalAttackResult),
			(int)result.Outcome,
			result.TargetType,
			result.HitSoundKey,
			result.BreakSoundKey,
			result.HitPoint
		);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	public void ReceiveLocalAttackResult(int outcome, string targetType, string hitSoundKey, string breakSoundsKey,
		Vector3 hitPoint)
	{
		var result = new ToolHitResult(
			(ToolHitOutcome)outcome,
			targetType,
			hitSoundKey,
			breakSoundsKey,
			hitPoint);

		GameManager.Instance.LocalPlayer?.HandleLocalAttackReuslt(result);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	public void ReceivePlayerHitState(float health, Vector3 position, Vector3 knockbackVelocity)
	{
		var player = GameManager.Instance.LocalPlayer;
		if (player == null)
			return;

		player.ApplyRemoteHitState(health, position, knockbackVelocity);
	}
}