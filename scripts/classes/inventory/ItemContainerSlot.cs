using Godot;
using Microsoft.VisualBasic;
using System.Collections.Generic;

public partial class ItemContainerSlot : Panel
{
	public bool IsHotbar;
	public int Index;
	public HUD Hud;

	private TextureRect icon;
	private Label countLabel;

	public override void _Ready()
    {
        icon = GetNode<TextureRect>("Icon");
		countLabel = GetNode<Label>("Label");
    }

	public void SetDisplay(ItemStack stack)
    {
        if (stack == null)
		{
			icon.Texture = null;
			countLabel.Text = "";
		}
		else
		{
			icon.Texture = stack.Item.Icon;
			countLabel.Text = stack.Count > 1 ? stack.Count.ToString() : "";
		}
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.Left)
				if (Input.IsKeyPressed(Key.Shift))
					Hud.OnSlotShiftLeftClick(IsHotbar, Index);
				else
				Hud.OnSlotLeftClick(IsHotbar, Index);
				
			else if (mb.ButtonIndex == MouseButton.Right)
                Hud.OnSlotRightClick(IsHotbar, Index);
        }
    }
}
