using System;
using Godot;
public partial class ItemContainerSlot : PanelContainer
{
	public event Action<IItemContainer, int> SlotLeftClicked;
	public event Action<IItemContainer, int> SlotRightClicked;
	public event Action<IItemContainer, int> SlotShiftClicked;
	public event Action SlotHoldStarted;
	public event Action SlotHoldCompleted;

	private Tooltip tooltip;

	public ItemStack Stack { get; private set; }
	public IItemContainer Container { get; private set; }
	public int Index { get; private set; }

	public Label CountLabel;
	private TextureRect icon;

	public bool ReadOnly = false;
	public bool IsCraftingSlot = false;
	public int CompletedCount = 0;
	public int TotalCount = 0;

	public bool HoldToActivate = false;
	public float HoldDuration = 1f;
	private bool isHolding = false;
	private double holdProgress = 0f;
	private ProgressBar holdProgressBar;
	private bool isHovered = false;


	public override void _Ready()
	{
		tooltip = GetTree().Root.GetNode<HUD>("Game/HUD").GetNode<Tooltip>("TooltipManager");
		icon = GetNode<TextureRect>("Icon");
		CountLabel = GetNode<Label>("Label");
		holdProgressBar = GetNode<ProgressBar>("ProgressBar");

		MouseExited += OnMouseExited;
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
		CompletedCount = completed;
		TotalCount = total;
		UpdateDisplay();
	}

	public void UpdateDisplay()
	{
		if (icon == null || CountLabel == null) return;

		if (Stack == null || Stack.Item == null)
		{
			icon.Texture = null;
			CountLabel.Text = "";
			TooltipText = "";
			return;
		}

		icon.Texture = Stack.Item.Icon;

		if (IsCraftingSlot)
			CountLabel.Text = $"{CompletedCount}/{TotalCount}";
		else
			CountLabel.Text = Stack.Count > 1 ? Stack.Count.ToString() : "";
	}

	public override void _GuiInput(InputEvent e)
	{
		if (e is InputEventMouseMotion && Stack != null && Stack.Item != null)
		{
			if (!isHovered)
			{
				isHovered = true;
				tooltip.ShowTooltip(Stack.Item, this);
			}
		}

		if (ReadOnly) return;

		if (e is InputEventMouseButton mb)
		{
			// Handle hold to activate
			if (HoldToActivate)
			{
				isHolding = true;
				holdProgress = 0f;
				SlotHoldStarted?.Invoke();

				if (mb.ButtonIndex == MouseButton.Left && !mb.Pressed)
				{
					isHolding = false;
					holdProgress = 0f;
					if (holdProgressBar != null)
						holdProgressBar.Visible = false;
				}

				return;
			}

			// Handle normal clicks
			if (mb.Pressed)
			{
				tooltip.HideTooltip();
				if (mb.ButtonIndex == MouseButton.Left)
				{
					if (Input.IsKeyPressed(Key.Shift))
						SlotShiftClicked?.Invoke(Container, Index);
					else
						SlotLeftClicked?.Invoke(Container, Index);
				}
				else if (mb.ButtonIndex == MouseButton.Right)
				{
					SlotRightClicked?.Invoke(Container, Index);
				}
			}
		}
	}

	public override void _Process(double delta)
	{
		if (!HoldToActivate || !isHolding) return;

		holdProgress += delta;

		if (holdProgressBar != null)
		{
			float raw = (float)(holdProgress / HoldDuration);
			float pct = 1f - Mathf.Pow(1f - raw, 3);
			holdProgressBar.Visible = true;
			holdProgressBar.Value = Mathf.Clamp(pct * 100f, 0, 100f);
		}

		if (holdProgress >= HoldDuration)
		{
			isHolding = false;
			holdProgress = 0f;

			if (holdProgressBar != null)
				holdProgressBar.Visible = false;

			SlotHoldCompleted?.Invoke();
		}
	}

	private void OnMouseExited()
	{
		isHovered = false;
		tooltip.HideTooltip(this);
	}
}