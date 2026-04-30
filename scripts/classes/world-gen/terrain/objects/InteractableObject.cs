using Godot;
using System;

public abstract partial class InteractableObject : WorldObject, IInteractable
{
	protected InteractionPrompt InteractPrompt;

	public virtual bool CanInteract(Player player)
	{
		return GlobalPosition.DistanceTo(player.GlobalPosition) <= 2f;
	}

	public virtual void OnFocusGained(Player player)
	{
		UpdateFocus(player);
	}

	public virtual void OnFocusLost(Player player)
	{
		InteractPrompt.HideIcon();
	}

	public virtual void UpdateFocus(Player player)
	{
		if (CanInteract(player))
			InteractPrompt?.ShowIcon();
		else
			InteractPrompt?.HideIcon();
	}

	public abstract void Interact(Player player);
}