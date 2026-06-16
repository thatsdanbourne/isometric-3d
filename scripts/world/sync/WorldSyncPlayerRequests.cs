using Godot;
using System;

public partial class WorldSync
{
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	public void RequestSelectHotbarSlot(int slotIndex)
	{
		if (!Multiplayer.IsServer())
			return;

		var player = GetRequestingPlayer();
		if (player == null)
			return;

		player.HandleSelectedSlotChanged(slotIndex);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false)]
	public void RequestUseActiveTool(Vector3 swingDir, bool isCharged)
	{
		if (!Multiplayer.IsServer())
			return;

		var player = GetRequestingPlayer();
		if (player == null)
			return;

		_world.HandleUseActiveToolRequest(player, swingDir, isCharged);
	}

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
}