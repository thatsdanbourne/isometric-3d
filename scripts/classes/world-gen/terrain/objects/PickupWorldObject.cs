using Godot;
using System;

public partial class PickupWorldObject : WorldObject, IInteractable
{
	public override bool CanReceiveToolHits => false;

	public void OnFocusGained()
	{
	}

	public void OnFocusLost()
	{
	}

	public void Interact(Player player)
	{
		if (!CanInteract(player))
			return;

		if (!World.Multiplayer.IsServer())
		{
			World.Sync.RpcId(1, nameof(World.Sync.RequestInteractObject), Data.ChunkCoord, Data.TileCoord);
			return;
		}

		BreakObject();
	}

	public bool CanInteract(Player player)
	{
		return true;
	}
}