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
		GD.Print("Pickup interacted");
	}

	public bool CanInteract(Player player)
	{
		return true;
	}
}