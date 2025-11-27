using Godot;

public partial class Tooltip : Control
{
	private Control tooltip;
	private Label itemName;
	private Label description;
	private Label damage;

	private Tween fadeTween;
	private float delay = 0.5f;
	private float fadeDuration = 0.5f;
	private bool isShowing = false;
	private bool waitingToShow = false;

	private Control currentSlot;
	private Control hoveredSlot;

	public override void _Ready()
    {
		tooltip = GetNode<Control>("Tooltip");
		itemName = tooltip.GetNode<Label>("VBoxContainer/Name");
		damage = tooltip.GetNode<Label>("VBoxContainer/MarginContainer/VBoxContainer/Damage");
		description = tooltip.GetNode<Label>("VBoxContainer/MarginContainer/VBoxContainer/Description");

		tooltip.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
		tooltip.Visible = false;
    }

	public async void ShowTooltip(Item item, Control slot)
    {
		if (hoveredSlot != slot)
        {
            hoveredSlot = slot;
			waitingToShow = false;
			fadeTween?.Kill();

			if (isShowing)
            {
                FillContent(item);
				PositionTooltip(slot);
				FadeInInstant();
				currentSlot = slot;
				return;
            }
        }

		waitingToShow = true;

		await ToSignal(GetTree().CreateTimer(delay), SceneTreeTimer.SignalName.Timeout);

		if (!waitingToShow || hoveredSlot != slot) return;

		FillContent(item);
		PositionTooltip(slot);

		FadeIn();
		currentSlot = slot;
		isShowing = true;
		return;
	}

	private void FillContent(Item item)
	{
		itemName.Text = item.DisplayName;

		if (string.IsNullOrEmpty(item.Description))
			description.Visible = false;
		else
		{
			description.Visible = true;
			description.Text = item.Description;
		}

		if (item is ToolItem tool)
		{
			damage.Visible = true;
			damage.Text = $"Damage: {tool.Damage}";
		}
		else
		{
			damage.Visible = false;
		}

		tooltip.ResetSize();
	}

	private void PositionTooltip(Control slot)
	{
		var slotRect = slot.GetGlobalRect();
		tooltip.GlobalPosition = new Vector2(
			slotRect.Position.X + slotRect.Size.X / 2f - tooltip.Size.X / 2f,
			slotRect.Position.Y - tooltip.Size.Y
		);
	}

	private void FadeIn()
    {
        tooltip.Visible = true;
		tooltip.Modulate = new Color(1, 1, 1, 0);

		fadeTween = CreateTween();
		fadeTween.TweenProperty(tooltip, "modulate:a", 1f, fadeDuration)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);

		fadeTween.Parallel().TweenProperty(
		tooltip, "position:y",
		tooltip.Position.Y - 5,
		fadeDuration
		);
    }

	private void FadeInInstant()
    {
        tooltip.Visible = true;
		tooltip.Modulate = new Color(1, 1, 1, 1);
    }

	private void FadeOut()
	{
		isShowing = false;

		fadeTween = CreateTween();
		fadeTween.TweenProperty(tooltip, "modulate:a", 0f, fadeDuration)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.In);
		
		fadeTween.TweenCallback(Callable.From(() =>
		{
			tooltip.Visible = false;
			currentSlot = null;
		}));
	}

	public void HideTooltip(Control slot = null)
	{
		if (hoveredSlot == slot)
            hoveredSlot = null;
		
		if (hoveredSlot != null)
			return;
		
		waitingToShow = false;

		if (!isShowing)
			return;

		fadeTween?.Kill();
		FadeOut();
		fadeTween = CreateTween();

		fadeTween.TweenProperty(tooltip, "modulate:a", 0f, fadeDuration)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.In);
			
		fadeTween.TweenCallback(Callable.From(() =>
		{
			tooltip.Visible = false;
			itemName.Text = "";
			description.Text = "";
			damage.Text = "";
		}));
	}
}

