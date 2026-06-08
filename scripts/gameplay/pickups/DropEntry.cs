using Godot;

[GlobalClass]
public partial class DropEntry : Resource
{
    [Export] public string ItemId;
    [Export] public int MinQuantity = 1;
    [Export] public int MaxQuantity = 1;
    [Export] public float Chance = 1f;

    public DropEntry() { }
    
    public DropEntry(string id, int minQty = 1, int maxQty = 1, float chance = 1f)
    {
        ItemId = id;
        MinQuantity = minQty;
        MaxQuantity = maxQty;
        Chance = chance;
    }
}
