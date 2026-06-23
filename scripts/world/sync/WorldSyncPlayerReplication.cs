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

	public void BroadcastMeleeHitResult(int attackerPlayerId, ToolHitResult result)
	{
		if (GameManager.Instance?.LocalPlayer != null)
			ApplyMeleeHitResult(attackerPlayerId, result);

		Rpc(
			nameof(ReceiveMeleeHitResult),
			attackerPlayerId,
			(int)result.Outcome,
			result.TargetType,
			result.PrimarySoundKey,
			result.BreakSoundKey,
			result.HitPoint,
			result.TargetPlayerId,
			result.TargetHealth,
			result.HitDirection,
			result.Knockback
		);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	public void ReceiveMeleeHitResult(int attackerPlayerId, int outcome, string targetType, string primarySoundKey,
		string breakSoundsKey,
		Vector3 hitPoint, int targetPlayerId, float targetHealth, Vector3 hitDirection, float knockback)
	{
		var result = new ToolHitResult(
			(ToolHitOutcome)outcome,
			targetType,
			primarySoundKey,
			breakSoundsKey,
			hitPoint,
			targetPlayerId,
			targetHealth,
			hitDirection,
			knockback);

		ApplyMeleeHitResult(attackerPlayerId, result);
	}

	private void ApplyMeleeHitResult(int attackerPlayerId, ToolHitResult result)
	{
		GameManager.Instance.LocalPlayer?.HandleAttackResult(result);

		if (result.HasPlayerTarget)
		{
			var player = _world.GetPlayerById(result.TargetPlayerId);
			player?.ApplyRemoteHitEvent(result.TargetHealth, result.HitDirection, result.Knockback);
		}

		if (attackerPlayerId == Multiplayer.GetUniqueId())
			GameManager.Instance.LocalPlayer?.HandleLocalAttackResult(result);
	}

	public void SyncCombatAnim(PlayerCombatAnimEvent animEvent, string toolId, Vector3 swingDir, int comboIndex,
		int sequence)
	{
		RpcId(
			1,
			nameof(RequestCombatAnim),
			(int)animEvent,
			toolId,
			swingDir,
			comboIndex,
			sequence
		);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	public void RequestCombatAnim(int animEvent, string toolId, Vector3 swingDir, int comboIndex, int sequence)
	{
		if (!Multiplayer.IsServer())
			return;

		var senderId = Multiplayer.GetRemoteSenderId();
		Rpc(nameof(ReceiveCombatAnim), senderId, animEvent, toolId, swingDir, comboIndex, sequence);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
	private void ReceiveCombatAnim(int playerId, int animEvent, string toolId, Vector3 swingDir, int comboIndex,
		int sequence)
	{
		var player = _world.GetPlayerById(playerId);

		if (player == null || player.IsLocal)
			return;

		if (ItemRegistry.GetItem(toolId) is not ToolItem tool)
			return;

		player.PlayRemoteCombatAnim((PlayerCombatAnimEvent)animEvent, tool, swingDir, comboIndex, sequence);
	}

	public void SetBlocking(bool blocking)
	{
		RpcId(1, nameof(RequestBlockingState), blocking);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer)]
	private void RequestBlockingState(bool blocking)
	{
		var senderId = Multiplayer.GetRemoteSenderId();

		var player = _world.GetPlayerById(senderId);
		if (player == null)
			return;

		if (blocking)
			player.CombatController.StartBlock();
		else
			player.CombatController.EndBlock();

		Rpc(nameof(ReceiveBlockingState), senderId, blocking);
	}

	[Rpc]
	private void ReceiveBlockingState(int playerId, bool blocking)
	{
		var player = _world.GetPlayerById(playerId);
		if (player == null)
			return;

		if (blocking)
			player.CombatController.StartBlock();
		else
			player.CombatController.EndBlock();
	}
}
