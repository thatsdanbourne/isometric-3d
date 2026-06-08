using Godot;

public struct ItemPickupSpawnData
{
	public ulong PickupId;
	public string ItemId;
	public int Count;
	public Vector3 Position;
	public Vector3 InitialVelocity;
	public float InitialVerticalVelocity;
}