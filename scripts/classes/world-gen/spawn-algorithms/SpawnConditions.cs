using System.Collections.Generic;

public class SpawnConditions
{
	public List<NeighbourRequirement> NeighbourRequirements { get; set; } = new();
	public List<DensityModifier> DensityModifiers { get; set; } = new();
}

public class NeighbourRequirement
{
	public NeighbourTargetType TargetType { get; set; }
	public string TargetId { get; set; } = "";
	public int Radius { get; set; } = 1;
	public int MinCount { get; set; } = 1;
	public int MaxCount { get; set; } = int.MaxValue;
}

public class DensityModifier
{
	public NeighbourTargetType TargetType { get; set; }
	public string TargetId { get; set; } = "";

	public int Radius { get; set; } = 1;

	public float MinMultiplier { get; set; } = 1f;
	public float MaxMultiplier { get; set; } = 1f;

	public int MinCount { get; set; } = 1;
	public int MaxCount { get; set; } = 8;

	public DistanceFalloffMode FalloffMode { get; set; } = DistanceFalloffMode.Linear;
}