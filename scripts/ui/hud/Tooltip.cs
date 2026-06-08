using Godot;

public partial class Tooltip : Control
{
	private Control _tooltip;
	private Label _itemName;
	private Label _description;
	private Label _damage;

	private Tween _fadeTween;
	private float _delay = 0.5f;
	private float _fadeDuration = 0.5f;
	private bool _isShowing;
	private bool _waitingToShow;

	private Control _currentSlot;
	private Control _hoveredSlot;

	public override void _Ready()
	{
		_tooltip = GetNode<Control>("Tooltip");
		_itemName = _tooltip.GetNode<Label>("VBoxContainer/Name");
		_damage = _tooltip.GetNode<Label>("VBoxContainer/MarginContainer/VBoxContainer/Damage");
		_description = _tooltip.GetNode<Label>("VBoxContainer/MarginContainer/VBoxContainer/Description");

		_tooltip.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
		_tooltip.Visible = false;
	}

	public async void ShowTooltip(Item item, Control slot)
	{
		if (_hoveredSlot != slot)
		{
			_hoveredSlot = slot;
			_waitingToShow = false;
			_fadeTween?.Kill();

			if (_isShowing)
			{
				FillContent(item);
				PositionTooltip(slot);
				FadeInInstant();
				_currentSlot = slot;
				return;
			}
		}

		_waitingToShow = true;

		await ToSignal(GetTree().CreateTimer(_delay), SceneTreeTimer.SignalName.Timeout);

		if (!_waitingToShow || _hoveredSlot != slot) return;

		FillContent(item);
		PositionTooltip(slot);

		// FadeIn();
		FadeInInstant();
		_currentSlot = slot;
		_isShowing = true;
	}

	private void FillContent(Item item)
	{
		_itemName.Text = item.DisplayName;

		if (string.IsNullOrEmpty(item.Description))
		{
			_description.Visible = false;
		}
		else
		{
			_description.Visible = true;
			_description.Text = item.Description;
		}

		if (item is ToolItem tool)
		{
			_damage.Visible = true;
			_damage.Text = $"Damage: {tool.Damage}";
		}
		else
		{
			_damage.Visible = false;
		}

		_tooltip.ResetSize();
	}

	private void PositionTooltip(Control slot)
	{
		var slotRect = slot.GetGlobalRect();
		_tooltip.GlobalPosition = new Vector2(
			slotRect.Position.X + slotRect.Size.X / 2f - _tooltip.Size.X / 2f,
			slotRect.Position.Y - _tooltip.Size.Y
		);
	}

	private void FadeIn()
	{
		_tooltip.Visible = true;
		_tooltip.Modulate = new Color(1, 1, 1, 0);

		_fadeTween = CreateTween();
		_fadeTween.TweenProperty(_tooltip, "modulate:a", 1f, _fadeDuration)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);

		_fadeTween.Parallel().TweenProperty(
			_tooltip, "position:y",
			_tooltip.Position.Y - 5,
			_fadeDuration
		);
	}

	private void FadeInInstant()
	{
		_tooltip.Visible = true;
		_tooltip.Modulate = new Color(1, 1, 1);
	}

	private void FadeOut()
	{
		_isShowing = false;

		_fadeTween = CreateTween();
		_fadeTween.TweenProperty(_tooltip, "modulate:a", 0f, _fadeDuration)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.In);

		_fadeTween.TweenCallback(Callable.From(() =>
		{
			_tooltip.Visible = false;
			_currentSlot = null;
		}));
	}

	public void HideTooltip(Control slot = null)
	{
		if (_hoveredSlot == slot)
			_hoveredSlot = null;

		if (_hoveredSlot != null)
			return;

		_waitingToShow = false;

		if (!_isShowing)
			return;

		// fadeTween?.Kill();
		// FadeOut();
		// fadeTween = CreateTween();

		// fadeTween.TweenProperty(tooltip, "modulate:a", 0f, fadeDuration)
		// 	.SetTrans(Tween.TransitionType.Cubic)
		// 	.SetEase(Tween.EaseType.In);

		// fadeTween.TweenCallback(Callable.From(() =>
		// {
		// 	tooltip.Visible = false;
		// 	itemName.Text = "";
		// 	description.Text = "";
		// 	damage.Text = "";
		// }));

		_tooltip.Visible = false;
		_itemName.Text = "";
		_description.Text = "";
		_damage.Text = "";
		_isShowing = false;
	}
}