using Godot;
using System;

public partial class PickupWorldObject : InteractableObject
{
	private PackedScene _interactPromptScene = GD.Load<PackedScene>("res://scenes/ui/HUD/InteractionPrompt.tscn");

	public override bool CanReceiveToolHits => false;

	public override void _Ready()
	{
		base._Ready();
		InteractPrompt = _interactPromptScene.Instantiate<InteractionPrompt>();
		InteractPrompt.Position = new Vector3(0f, 1.5f, 0f);
		AddChild(InteractPrompt);
	}

	public override void Interact(Player player)
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
}