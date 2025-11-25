using Godot;
using Microsoft.VisualBasic;
using System.Collections.Generic;

public partial class HotbarSlot : Panel
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
			{
				Hud.OnSlotLeftClick(IsHotbar, Index);
			}
			else if (mb.ButtonIndex == MouseButton.Right)
            {
                Hud.OnSlotRightClick(IsHotbar, Index);
            }
        }
    }


    // public override Variant _GetDragData(Vector2 atPosition)
    // {
	// 	var stack = Hud.GetStack(IsHotbar, Index);
	// 	if (stack == null)
	// 		return Variant.CreateFrom(false);
		
	// 	var data = new Godot.Collections.Dictionary<string, Variant>
    //     {
    //         { "is_hotbar", IsHotbar },
	// 		{ "index", Index },
    //     };

	// 	var preview = Duplicate() as Control;
	// 	if (preview != null)
	// 		SetDragPreview(preview);
		
	// 	return data;
	// }

    // public override bool _CanDropData(Vector2 atPosition, Variant data)
    // {
	// 	if (!data.VariantType.Equals(Variant.Type.Dictionary))
	// 		return false;
		
	// 	var dict = data.AsGodotDictionary<string, Variant>();
	// 	return dict.ContainsKey("is_hotbar") && dict.ContainsKey("index");
    // }

    // public override void _DropData(Vector2 atPosition, Variant data)
    // {
    //     var dict = data.AsGodotDictionary<string, Variant>();
	// 	bool srcHotbar = (bool)dict["is_hotbar"];
	// 	int srcIndex = (int)dict["index"];

	// 	Hud.HandleSlotDrop(srcHotbar, srcIndex, IsHotbar, Index);
    // }
}
