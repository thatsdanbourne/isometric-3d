public abstract partial class InteractableObject : WorldObject, IInteractable
{
	private float _interactRange = 2f;
	protected InteractionPrompt InteractPrompt;

	public virtual bool CanInteract(Player player)
	{
		return GlobalPosition.DistanceTo(player.GlobalPosition) <= _interactRange;
	}

	public virtual void OnFocusGained(Player player)
	{
		UpdateFocus(player);
	}

	public virtual void OnFocusLost(Player player)
	{
		SetPromptVisible(false);
	}

	public virtual void UpdateFocus(Player player)
	{
		SetPromptVisible(CanInteract(player));
	}

	private void SetPromptVisible(bool visible)
	{
		if (visible)
			InteractPrompt?.ShowIcon();
		else
			InteractPrompt?.HideIcon();
	}

	public abstract void Interact(Player player);
}