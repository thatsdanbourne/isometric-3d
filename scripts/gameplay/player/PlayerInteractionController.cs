using Godot;

public partial class PlayerInteractionController : Node
{
	private const float FocusHz = 20f;
	private const float FocusInterval = 1f / FocusHz;
	private const uint InteractableMask = 1u << 2;

	private Player _player;
	private PhysicsRayQueryParameters3D _focusQuery;
	private float _focusAccum;

	public IInteractable FocusedInteractable { get; private set; }

	public void Init(Player player)
	{
		_player = player;

		_focusQuery = new PhysicsRayQueryParameters3D
		{
			CollideWithAreas = false,
			CollideWithBodies = true,
			CollisionMask = InteractableMask
		};
	}

	public void Tick(float dt, Camera3D camera, Vector2 mousePos, bool canFocus)
	{
		if (!canFocus || camera == null)
			return;

		_focusAccum += dt;
		if (_focusAccum < FocusInterval)
			return;

		_focusAccum -= FocusInterval;
		UpdateFocusedObject(camera, mousePos);
	}

	public void TryInteract(bool canInteract)
	{
		if (!canInteract)
			return;

		if (FocusedInteractable != null && FocusedInteractable.CanInteract(_player))
			FocusedInteractable.Interact(_player);
	}

	private void UpdateFocusedObject(Camera3D camera, Vector2 mousePos)
	{
		var rayOrigin = camera.ProjectRayOrigin(mousePos);
		var rayDir = camera.ProjectRayNormal(mousePos);

		_focusQuery.From = rayOrigin;
		_focusQuery.To = rayOrigin + rayDir * 100f;

		var result = _player.GetWorld3D().DirectSpaceState.IntersectRay(_focusQuery);

		IInteractable newFocus = null;

		if (result.Count > 0 && result.TryGetValue("collider", out var col))
		{
			var node = col.As<Node>();
			newFocus = FindInteractable(node);
		}

		if (newFocus == FocusedInteractable)
		{
			FocusedInteractable?.UpdateFocus(_player);
			return;
		}

		FocusedInteractable?.OnFocusLost(_player);
		FocusedInteractable = newFocus;
		FocusedInteractable?.OnFocusGained(_player);
		FocusedInteractable?.UpdateFocus(_player);
	}

	private static IInteractable FindInteractable(Node node)
	{
		for (var n = node; n != null; n = n.GetParent())
			if (n is IInteractable interactable)
				return interactable;

		return null;
	}
}