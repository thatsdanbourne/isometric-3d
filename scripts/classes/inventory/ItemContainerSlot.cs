using System;
using Godot;

public partial class ItemContainerSlot : PanelContainer
{
	public event Action<IItemContainer, int> SlotLeftClicked;
	public event Action<IItemContainer, int> SlotRightClicked;
	public event Action<IItemContainer, int> SlotShiftClicked;
	public event Action SlotHoldStarted;
	public event Action SlotHoldCompleted;

	public Panel Highlight;
	private Tooltip _tooltip;

	private ItemStack Stack { get; set; }
	private IItemContainer Container { get; set; }
	private int Index { get; set; }

	public Label CountLabel;
	private TextureRect _icon;

	public bool ReadOnly = false;
	public bool IsCraftingSlot = false;
	private int _completedCount;
	private int _totalCount;

	public bool HoldToActivate = false;
	private float _holdDuration = 1f;
	private bool _isHolding;
	private double _holdProgress;
	private ProgressBar _holdProgressBar;

	public Tween ScaleTween;

	private StyleBoxFlat _slotStyle =
		ResourceLoader.Load<StyleBoxFlat>("res://resources/ui/ItemContainerSlotStyle.tres");

	private StyleBoxFlat _slotFilledStyle =
		ResourceLoader.Load<StyleBoxFlat>("res://resources/ui/ItemContainerSlotFilledStyle.tres");


	public override void _Ready()
	{
		_tooltip = GetTree().Root.GetNode<HUD>("Bootstrap/ClientUI/HUD").GetNode<Tooltip>("TooltipManager");
		Highlight = GetNode<Panel>("Highlight");
		Highlight.Visible = false;

		_icon = GetNode<TextureRect>("Icon");
		CountLabel = GetNode<Label>("Label");
		_holdProgressBar = GetNode<ProgressBar>("ProgressBar");

		MouseEntered += OnMouseEntered;
		MouseExited += OnMouseExited;

		SetProcess(false);
	}

	public void SetSlot(IItemContainer container, int index)
	{
		Container = container;
		Index = index;
	}

	public void SetStack(ItemStack stack)
	{
		Stack = stack;
		UpdateDisplay();
	}

	public void SetCraftingStack(Item item, int completed, int total)
	{
		Stack = new ItemStack(item, 0);
		_completedCount = completed;
		_totalCount = total;
		UpdateDisplay();
	}

	private void UpdateDisplay()
	{
		if (_icon == null || CountLabel == null) return;

		if (Stack?.Item == null)
		{
			AddThemeStyleboxOverride("panel", _slotStyle);
			_icon.Texture = null;
			CountLabel.Text = "";
			TooltipText = "";
			return;
		}

		AddThemeStyleboxOverride("panel", _slotFilledStyle);
		_icon.Texture = Stack.Item.Icon;

		if (IsCraftingSlot)
			CountLabel.Text = $"{_completedCount}/{_totalCount}";
		else
			CountLabel.Text = Stack.Count > 1 ? Stack.Count.ToString() : "";
	}

	public override void _GuiInput(InputEvent e)
	{
		if (ReadOnly) return;

		if (e is not InputEventMouseButton mb) return;

		// Handle hold to activate
		if (HoldToActivate)
		{
			if (mb.ButtonIndex == MouseButton.Left && mb.Pressed)
			{
				_isHolding = true;
				_holdProgress = 0f;
				SetProcess(true);
				SlotHoldStarted?.Invoke();
				if (_holdProgressBar != null) _holdProgressBar.Visible = true;
			}

			if (mb.ButtonIndex == MouseButton.Left && !mb.Pressed)
			{
				_isHolding = false;
				_holdProgress = 0f;
				if (_holdProgressBar != null) _holdProgressBar.Visible = false;
				SetProcess(false);
			}

			return;
		}

		// Handle normal clicks
		if (!mb.Pressed) return;

		_tooltip.HideTooltip();
		switch (mb.ButtonIndex)
		{
			case MouseButton.Left when Input.IsKeyPressed(Key.Shift):
				SlotShiftClicked?.Invoke(Container, Index);
				break;
			case MouseButton.Left:
				SlotLeftClicked?.Invoke(Container, Index);
				break;
			case MouseButton.Right:
				SlotRightClicked?.Invoke(Container, Index);
				break;
		}
	}

	public override void _Process(double delta)
	{
		if (!HoldToActivate || !_isHolding) return;

		_holdProgress += delta;

		if (_holdProgressBar != null)
		{
			var raw = (float)(_holdProgress / _holdDuration) * 100f;
			_holdProgressBar.Visible = true;
			_holdProgressBar.Value = raw;
		}

		if (!(_holdProgress >= _holdDuration)) return;

		_isHolding = false;
		_holdProgress = 0f;
		if (_holdProgressBar != null) _holdProgressBar.Visible = false;
		SetProcess(false);
		SlotHoldCompleted?.Invoke();
	}

	private void OnMouseEntered()
	{
		if (Stack?.Item == null) return;
		_tooltip.ShowTooltip(Stack.Item, this);
	}

	private void OnMouseExited()
	{
		_tooltip.HideTooltip(this);
	}
}