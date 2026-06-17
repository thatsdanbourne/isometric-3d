using Godot;

public partial class WorldSync
{
	public void SyncTransform(Player player)
	{
		if (player == null || !player.IsLocal)
			return;

		RpcId(
			1,
			nameof(RequestPlayerTransform),
			player.GlobalPosition,
			player.Velocity,
			player.Rotation.Y
		);
	}
	
	[Rpc(
		MultiplayerApi.RpcMode.AnyPeer,
		CallLocal = false,
		TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable
	)]
	private void RequestPlayerTransform(Vector3 pos, Vector3 vel, float rotY)
	{
		if (!Multiplayer.IsServer())
			return;

		var senderId = Multiplayer.GetRemoteSenderId();
		var player = _world.GetPlayerById(senderId);

		if (player == null)
			return;

		player.GlobalPosition = pos;
		player.Velocity = vel;
		player.Rotation = new Vector3(player.Rotation.X, rotY, player.Rotation.Z);

		Rpc(
			nameof(ReceivePlayerTransform),
			senderId,
			pos,
			vel,
			rotY
		);
	}
	
	[Rpc(
		MultiplayerApi.RpcMode.Authority,
		CallLocal = false,
		TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable
	)]
	private void ReceivePlayerTransform(int playerId, Vector3 pos, Vector3 vel, float rotY)
	{
		var player = _world.GetPlayerById(playerId);
		if (player == null)
			return;

		player.ApplyRemoteTransform(pos, vel, rotY);
	}
	
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

	public void SendPlayerHitEvent(Player player, Vector3 hitDirection, float knockback)
	{
		if (!Multiplayer.IsServer())
			return;

		Rpc(nameof(ReceivePlayerHitEvent), player.PlayerId, player.Health, hitDirection, knockback);
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

		GameManager.Instance.LocalPlayer?.HandleLocalAttackResult(result);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void ReceivePlayerHitEvent(int playerId, float health, Vector3 hitDirection, float knockback)
	{
		var player = _world.GetPlayerById(playerId);
		if (player == null)
			return;

		player.ApplyRemoteHitEvent(health, hitDirection, knockback);
	}
	
	public void SyncCombatAnim(PlayerCombatAnimEvent animEvent, string toolId, Vector3 swingDir)
	{
		RpcId(
			1,
			nameof(RequestCombatAnim),
			(int)animEvent,
			toolId,
			swingDir
		);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	public void RequestCombatAnim(int animEvent, string toolId, Vector3 swingDir)
	{
		if (!Multiplayer.IsServer())
			return;

		var senderId = Multiplayer.GetRemoteSenderId();
		Rpc(nameof(ReceiveCombatAnim), senderId, animEvent, toolId, swingDir);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void ReceiveCombatAnim(int playerId, int animEvent, string toolId, Vector3 swingDir)
	{
		var player = _world.GetPlayerById(playerId);

		if (player == null || player.IsLocal)
			return;

		if (ItemRegistry.GetItem(toolId) is not ToolItem tool)
			return;

		player.PlayRemoteCombatAnim((PlayerCombatAnimEvent)animEvent, tool, swingDir);
	}
}