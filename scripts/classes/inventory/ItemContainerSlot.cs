using System;
using Godot;

public partial class ItemContainerSlot : Panel
{
	[Signal] public delegate void SlotLeftClickedEventHandler(bool isHotbar, int index);
	[Signal] public delegate void SlotRightClickedEventHandler(bool isHotbar, int index);
	[Signal] public delegate void SlotShiftClickedEventHandler(bool isHotbar, int index);
	[Signal] public delegate void SlotHoldStartedEventHandler();
	[Signal] public delegate void SlotHoldCompletedEventHandler();

	public bool IsHotbar = false;
	public int Index;
	public bool ReadOnly = false;

	public bool HoldToActivate = false;
	public float HoldDuration = 1f;
	public Label CountLabel;

	private bool isHolding = false;
	private double holdProgress = 0f;
	private TextureRect icon;
	private ProgressBar holdProgressBar;


	public override void _Ready()
    {
        icon = GetNode<TextureRect>("Icon");
		CountLabel = GetNode<Label>("Label");
		holdProgressBar = GetNode<ProgressBar>("ProgressBar");
    }

    public override void _GuiInput(InputEvent e)
    {
		if (ReadOnly) return;

		if (e is InputEventMouseButton mb)
        {
			// Handle hold to activate
            if (HoldToActivate)
            {
                isHolding = true;
				holdProgress = 0f;
				EmitSignal(SignalName.SlotHoldStarted);

				if (mb.ButtonIndex == MouseButton.Left && !mb.Pressed)
				{
					isHolding = false;
					holdProgress = 0f;
					if(holdProgressBar != null)
						holdProgressBar.Visible = false;
				}

			return;
            }
        
		 	// Handle normal clicks
			if (mb.Pressed)
			{
				if(mb.ButtonIndex == MouseButton.Left)
				{
					if (Input.IsKeyPressed(Key.Shift))
					EmitSignal(SignalName.SlotShiftClicked, IsHotbar, Index);
					else
					EmitSignal(SignalName.SlotLeftClicked, IsHotbar, Index);
				}
				else if (mb.ButtonIndex == MouseButton.Right)
				{
					EmitSignal(SignalName.SlotRightClicked, IsHotbar, Index);
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

			if(holdProgressBar != null)
            	holdProgressBar.Visible = false;
			
			EmitSignal(SignalName.SlotHoldCompleted);
        }
    }
}