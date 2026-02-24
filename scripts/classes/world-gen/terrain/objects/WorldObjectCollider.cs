using Godot;

public partial class WorldObjectCollider : StaticBody3D, IInteractable
{
	public WorldObject ObjectOwner { get; private set; }
	private IInteractable _interactableOwner;

	public override void _Ready()
	{
		ObjectOwner = GetParent() as WorldObject;

		if (ObjectOwner == null)
		{
			GD.PushError($"{Name}: parent is not a WorldObject");
			return;
		}

		_interactableOwner = ObjectOwner as IInteractable;
	}

	public void OnFocusGained()
	{
		_interactableOwner?.OnFocusGained();
	}

	public void OnFocusLost()
	{
		_interactableOwner?.OnFocusLost();
	}

	public T GetCapability<T>() where T : class
	{
		return _interactableOwner?.GetCapability<T>();
	}
}